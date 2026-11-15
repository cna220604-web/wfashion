using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers;

[ApiController]
[Authorize]
[AutoValidateAntiforgeryToken]
[Route("api/support-chat")]
public sealed class SupportChatController(
    ApplicationDbContext database,
    UserManager<ApplicationUser> userManager,
    SupportChatPresence presence) : ControllerBase
{
    [HttpGet("availability")]
    [Authorize(Roles = AppRoles.Employee + "," + AppRoles.Admin)]
    public IActionResult GetAvailability()
    {
        var userId = userManager.GetUserId(User);
        return Ok(new { online = userId is not null && presence.IsOnline(userId) });
    }

    [HttpPost("availability")]
    [Authorize(Roles = AppRoles.Employee + "," + AppRoles.Admin)]
    public IActionResult SetAvailability([FromBody] AvailabilityRequest request)
    {
        var userId = userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        if (request.Online)
        {
            presence.SetOnline(userId);
        }
        else
        {
            presence.SetOffline(userId);
        }

        return Ok(new { online = request.Online });
    }

    [HttpGet("current")]
    [Authorize(Roles = AppRoles.Customer)]
    public async Task<IActionResult> Current([FromHeader(Name = "X-Client-Session")] string? clientSessionId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParseExact(clientSessionId, "D", out var sessionId))
        {
            return BadRequest(new { error = "Phiên tab không hợp lệ. Hãy tải lại trang và thử lại." });
        }

        var customerId = userManager.GetUserId(User)!;
        var conversation = await GetCurrentCustomerConversationAsync(customerId, sessionId.ToString("D"), cancellationToken);
        if (conversation is null)
        {
            return Ok(new { conversation = (SupportConversationView?)null });
        }

        return Ok(new { conversation = await ToConversationViewAsync(conversation, cancellationToken) });
    }

    [HttpPost("start")]
    [Authorize(Roles = AppRoles.Customer)]
    public async Task<IActionResult> Start(
        [FromBody] StartSupportChatRequest request,
        [FromHeader(Name = "X-Client-Session")] string? clientSessionId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParseExact(clientSessionId, "D", out var sessionId))
        {
            return BadRequest(new { error = "Phiên tab không hợp lệ. Hãy tải lại trang và thử lại." });
        }

        if (request.Messages.Any(message => string.IsNullOrWhiteSpace(message.Content)))
        {
            return BadRequest(new { error = "Không thể chuyển tiếp tin nhắn trống." });
        }

        var customerId = userManager.GetUserId(User)!;
        var existing = await GetCurrentCustomerConversationAsync(customerId, sessionId.ToString("D"), cancellationToken);
        if (existing is not null)
        {
            return Ok(new { conversation = await ToConversationViewAsync(existing, cancellationToken) });
        }

        var customer = await userManager.GetUserAsync(User);
        if (customer is null)
        {
            return Unauthorized();
        }

        var now = DateTime.UtcNow;
        var employeeId = presence.GetAvailableEmployee();
        var conversation = new SupportConversation
        {
            CustomerUserId = customer.Id,
            ClientSessionId = sessionId.ToString("D"),
            AssignedEmployeeUserId = employeeId,
            Status = employeeId is null ? SupportChatStatuses.Waiting : SupportChatStatuses.Active,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        database.SupportConversations.Add(conversation);
        foreach (var message in request.Messages)
        {
            database.SupportMessages.Add(new SupportMessage
            {
                Conversation = conversation,
                SenderUserId = customer.Id,
                SenderName = message.Role == "assistant" ? "Stylist AI" : customer.DisplayName,
                SenderRole = message.Role == "assistant" ? "Stylist" : "Customer",
                Content = message.Content.Trim(),
                CreatedAtUtc = now
            });
        }

        if (request.Messages.All(message => message.Role != "user"))
        {
            database.SupportMessages.Add(new SupportMessage
            {
                Conversation = conversation,
                SenderUserId = customer.Id,
                SenderName = customer.DisplayName,
                SenderRole = "Customer",
                Content = "Xin chào, tôi muốn được nhân viên tư vấn thêm.",
                CreatedAtUtc = now
            });
        }

        await database.SaveChangesAsync(cancellationToken);
        return Ok(new { conversation = await ToConversationViewAsync(conversation, cancellationToken) });
    }

    [HttpGet("inbox")]
    [Authorize(Roles = AppRoles.Employee + "," + AppRoles.Admin)]
    public async Task<IActionResult> Inbox(CancellationToken cancellationToken)
    {
        var employeeId = userManager.GetUserId(User)!;
        var conversations = await database.SupportConversations.AsNoTracking()
            .Where(item => item.Status != SupportChatStatuses.Closed &&
                (item.AssignedEmployeeUserId == employeeId || item.AssignedEmployeeUserId == null))
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);
        var customerIds = conversations.Select(item => item.CustomerUserId).Distinct().ToArray();
        var customers = await userManager.Users.Where(user => customerIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.DisplayName, cancellationToken);
        var conversationIds = conversations.Select(item => item.Id).ToArray();
        var messageCounts = await database.SupportMessages.AsNoTracking()
            .Where(message => conversationIds.Contains(message.ConversationId))
            .GroupBy(message => message.ConversationId)
            .Select(group => new { ConversationId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.ConversationId, item => item.Count, cancellationToken);

        var employeeIds = conversations.Where(item => item.AssignedEmployeeUserId is not null)
            .Select(item => item.AssignedEmployeeUserId!)
            .Distinct()
            .ToArray();
        var employees = await userManager.Users.Where(user => employeeIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.DisplayName, cancellationToken);
        var result = conversations.Select(item => new SupportConversationView(
            item.Id,
            customers.GetValueOrDefault(item.CustomerUserId) ?? "Khách hàng",
            item.Status,
            item.AssignedEmployeeUserId is null ? null : employees.GetValueOrDefault(item.AssignedEmployeeUserId),
            messageCounts.GetValueOrDefault(item.Id),
            item.UpdatedAtUtc));
        return Ok(new { conversations = result });
    }

    [HttpPost("{conversationId:int}/claim")]
    [Authorize(Roles = AppRoles.Employee + "," + AppRoles.Admin)]
    public async Task<IActionResult> Claim(int conversationId, CancellationToken cancellationToken)
    {
        var employeeId = userManager.GetUserId(User)!;
        var conversation = await database.SupportConversations
            .FirstOrDefaultAsync(item => item.Id == conversationId, cancellationToken);
        if (conversation is null)
        {
            return NotFound(new { error = "Cuộc trò chuyện không tồn tại." });
        }

        if (conversation.AssignedEmployeeUserId == employeeId && conversation.Status == SupportChatStatuses.Active)
        {
            return Ok(new { conversation = await ToConversationViewAsync(conversation, cancellationToken) });
        }

        var updated = await database.SupportConversations
            .Where(item => item.Id == conversationId && item.AssignedEmployeeUserId == null && item.Status == SupportChatStatuses.Waiting)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.AssignedEmployeeUserId, employeeId)
                .SetProperty(item => item.Status, SupportChatStatuses.Active)
                .SetProperty(item => item.UpdatedAtUtc, DateTime.UtcNow), cancellationToken);
        if (updated == 0)
        {
            return Conflict(new { error = "Cuộc trò chuyện vừa được một nhân viên khác nhận hoặc đã kết thúc." });
        }

        conversation = await database.SupportConversations.AsNoTracking()
            .FirstAsync(item => item.Id == conversationId, cancellationToken);
        return Ok(new { conversation = await ToConversationViewAsync(conversation, cancellationToken) });
    }

    [HttpGet("{conversationId:int}/messages")]
    public async Task<IActionResult> Messages(int conversationId, [FromQuery] int after = 0, CancellationToken cancellationToken = default)
    {
        var conversation = await database.SupportConversations.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == conversationId, cancellationToken);
        if (conversation is null)
        {
            return NotFound();
        }

        if (!await CanAccessAsync(conversation))
        {
            return Forbid();
        }

        var messages = await database.SupportMessages.AsNoTracking()
            .Where(message => message.ConversationId == conversationId && message.Id > Math.Max(after, 0))
            .OrderBy(message => message.Id)
            .Take(100)
            .Select(message => new SupportMessageView(message.Id, message.SenderName, message.SenderRole, message.Content, message.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return Ok(new { messages, status = conversation.Status });
    }

    [HttpPost("{conversationId:int}/messages")]
    public async Task<IActionResult> SendMessage(int conversationId, [FromBody] SupportMessageRequest request, CancellationToken cancellationToken)
    {
        var conversation = await database.SupportConversations
            .FirstOrDefaultAsync(item => item.Id == conversationId, cancellationToken);
        if (conversation is null)
        {
            return NotFound(new { error = "Cuộc trò chuyện không tồn tại." });
        }

        if (!await CanAccessAsync(conversation))
        {
            return Forbid();
        }

        if (conversation.Status == SupportChatStatuses.Closed)
        {
            return Conflict(new { error = "Cuộc trò chuyện đã kết thúc." });
        }

        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        var isCustomer = User.IsInRole(AppRoles.Customer);
        var now = DateTime.UtcNow;
        var message = new SupportMessage
        {
            ConversationId = conversationId,
            SenderUserId = user.Id,
            SenderName = user.DisplayName,
            SenderRole = isCustomer ? "Customer" : "Employee",
            Content = request.Content.Trim(),
            CreatedAtUtc = now
        };
        conversation.UpdatedAtUtc = now;
        database.SupportMessages.Add(message);
        await database.SaveChangesAsync(cancellationToken);
        return Ok(new { message = new SupportMessageView(message.Id, message.SenderName, message.SenderRole, message.Content, message.CreatedAtUtc) });
    }

    [HttpPost("{conversationId:int}/close")]
    [Authorize(Roles = AppRoles.Employee + "," + AppRoles.Admin)]
    public async Task<IActionResult> Close(int conversationId, CancellationToken cancellationToken)
    {
        var employeeId = userManager.GetUserId(User)!;
        var conversation = await database.SupportConversations.FirstOrDefaultAsync(item => item.Id == conversationId, cancellationToken);
        if (conversation is null)
        {
            return NotFound();
        }
        if (conversation.AssignedEmployeeUserId != employeeId || conversation.Status == SupportChatStatuses.Closed)
        {
            return Forbid();
        }

        conversation.Status = SupportChatStatuses.Closed;
        conversation.UpdatedAtUtc = DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        return Ok(new { status = conversation.Status });
    }

    private async Task<SupportConversation?> GetCurrentCustomerConversationAsync(
        string customerId,
        string clientSessionId,
        CancellationToken cancellationToken)
    {
        var conversation = await database.SupportConversations
            .Where(item => item.CustomerUserId == customerId &&
                item.Status != SupportChatStatuses.Closed &&
                (item.ClientSessionId == clientSessionId || item.ClientSessionId == string.Empty))
            .OrderByDescending(item => item.ClientSessionId == clientSessionId)
            .ThenByDescending(item => item.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (conversation is not null && string.IsNullOrEmpty(conversation.ClientSessionId))
        {
            conversation.ClientSessionId = clientSessionId;
            await database.SaveChangesAsync(cancellationToken);
        }

        return conversation;
    }

    private Task<bool> CanAccessAsync(SupportConversation conversation)
    {
        var userId = userManager.GetUserId(User);
        if (User.IsInRole(AppRoles.Customer))
        {
            return Task.FromResult(conversation.CustomerUserId == userId);
        }

        return Task.FromResult(
            (User.IsInRole(AppRoles.Employee) || User.IsInRole(AppRoles.Admin)) &&
            conversation.AssignedEmployeeUserId == userId);
    }

    private async Task<SupportConversationView> ToConversationViewAsync(SupportConversation conversation, CancellationToken cancellationToken)
    {
        var customer = await userManager.FindByIdAsync(conversation.CustomerUserId);
        var employee = conversation.AssignedEmployeeUserId is null
            ? null
            : await userManager.FindByIdAsync(conversation.AssignedEmployeeUserId);
        var count = await database.SupportMessages.CountAsync(message => message.ConversationId == conversation.Id, cancellationToken);
        return new SupportConversationView(conversation.Id, customer?.DisplayName ?? "Khách hàng", conversation.Status,
            employee?.DisplayName, count, conversation.UpdatedAtUtc);
    }

    public sealed class AvailabilityRequest
    {
        public bool Online { get; init; }
    }
}
