using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text.Encodings.Web;
using WebApplication1.Models;

namespace WebApplication1.Services;

public sealed class OrderNotificationService(IConfiguration configuration, ILogger<OrderNotificationService> logger)
{
    public async Task<OrderNotificationResult> NotifyOrderPlacedAsync(
        CustomerOrder order,
        string? customerEmail,
        CancellationToken cancellationToken)
    {
        var details = BuildOrderDetails(order);
        var customerNotified = await SendAsync(
            [customerEmail],
            $"W Studio đã nhận đơn hàng #{order.Id:D6}",
            $"<p>Xin chào {Encode(order.CustomerName)}, W Studio đã nhận đơn hàng của bạn.</p>{details}<p>Chúng tôi sẽ cập nhật trạng thái đơn hàng trong tài khoản của bạn.</p>",
            cancellationToken);

        var staffNotified = await SendAsync(
            GetStaffRecipients(),
            $"Đơn hàng mới #{order.Id:D6} · W Studio",
            $"<p>Có đơn hàng mới cần xác nhận.</p>{details}",
            cancellationToken);

        return new OrderNotificationResult(customerNotified, staffNotified);
    }

    public Task<bool> NotifyOrderStatusChangedAsync(
        CustomerOrder order,
        string? customerEmail,
        CancellationToken cancellationToken)
    {
        var details = BuildOrderDetails(order);
        return SendAsync(
            [customerEmail],
            $"Cập nhật đơn hàng #{order.Id:D6} · W Studio",
            $"<p>Xin chào {Encode(order.CustomerName)}, đơn hàng của bạn đã chuyển sang trạng thái <strong>{Encode(GetStatusText(order.Status))}</strong>.</p>{details}<p>Bạn có thể xem tiến trình mới nhất trong tài khoản W Studio.</p>",
            cancellationToken);
    }

    public Task<bool> SendPasswordResetAsync(string email, string resetUrl, CancellationToken cancellationToken) =>
        SendAsync(
            [email],
            "Đặt lại mật khẩu W Studio",
            $"<p>Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản W Studio của bạn.</p><p><a href=\"{Encode(resetUrl)}\">Đặt lại mật khẩu</a></p><p>Nếu bạn không yêu cầu thao tác này, hãy bỏ qua email.</p>",
            cancellationToken);

    private async Task<bool> SendAsync(
        IEnumerable<string?> recipients,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        var configuredRecipients = recipients
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Select(address => address!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (configuredRecipients.Length == 0)
        {
            logger.LogWarning("Email notification was not sent because no recipient is configured for {Subject}", subject);
            return false;
        }

        var host = configuration["Email:Smtp:Host"];
        var fromAddress = configuration["Email:Smtp:From"];
        if (string.IsNullOrWhiteSpace(host) || !MailAddress.TryCreate(fromAddress, out var from))
        {
            logger.LogWarning("Email notification was not sent because Email:Smtp:Host or Email:Smtp:From is not configured");
            return false;
        }

        var configuredPort = configuration["Email:Smtp:Port"];
        var port = 587;
        if (configuredPort is not null &&
            (!int.TryParse(configuredPort, NumberStyles.None, CultureInfo.InvariantCulture, out port) || port is < 1 or > 65535))
        {
            logger.LogError("Email notification was not sent because the SMTP port configuration is invalid");
            return false;
        }

        var sslSetting = configuration["Email:Smtp:EnableSsl"];
        var enableSsl = true;
        if (sslSetting is not null && !bool.TryParse(sslSetting, out enableSsl))
        {
            logger.LogError("Email notification was not sent because Email:Smtp:EnableSsl is invalid");
            return false;
        }

        SmtpClient client;
        try
        {
            client = new SmtpClient(host, port)
            {
                EnableSsl = enableSsl,
                Timeout = 10000
            };
        }
        catch (ArgumentException exception)
        {
            logger.LogError(exception, "Email notification was not sent because SMTP configuration is invalid");
            return false;
        }

        using (client)
        {
            var username = configuration["Email:Smtp:Username"];
            var password = configuration["Email:Smtp:Password"];
            if (!string.IsNullOrWhiteSpace(username))
            {
                client.Credentials = new NetworkCredential(username, password);
            }

            using var sendTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            sendTimeout.CancelAfter(TimeSpan.FromSeconds(10));
            var sent = false;
            foreach (var recipient in configuredRecipients)
            {
                if (!MailAddress.TryCreate(recipient, out var to))
                {
                    logger.LogError("Email notification has an invalid recipient address");
                    continue;
                }

                using var message = new MailMessage(from, to)
                {
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };
                try
                {
                    await client.SendMailAsync(message, sendTimeout.Token);
                    sent = true;
                }
                catch (SmtpException exception)
                {
                    logger.LogError(exception, "Unable to send email notification for {Subject}", subject);
                }
                catch (InvalidOperationException exception)
                {
                    logger.LogError(exception, "Unable to send email notification for {Subject}", subject);
                }
                catch (PlatformNotSupportedException exception)
                {
                    logger.LogError(exception, "Email delivery is not supported on this runtime");
                    return false;
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    logger.LogWarning("Email notification timed out for {Subject}", subject);
                    return sent;
                }
            }

            return sent;
        }
    }

    private string[] GetStaffRecipients() => (configuration["Email:StaffRecipients"] ?? string.Empty)
        .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string BuildOrderDetails(CustomerOrder order)
    {
        var lines = string.Join(string.Empty, order.Items.Select(item =>
            $"<li>{Encode(item.ProductName)} · {Encode(item.Size)} × {item.Quantity} — {(item.UnitPrice * item.Quantity).ToString("N0")}₫</li>"));
        return $"<ul>{lines}</ul><p>Tổng cộng: <strong>{order.Total.ToString("N0")}₫</strong><br>Thanh toán: COD<br>Địa chỉ: {Encode(order.Address)}</p>";
    }

    private static string GetStatusText(string status) => status switch
    {
        OrderStatuses.Pending => "Chờ xác nhận",
        OrderStatuses.Processing => "Đang chuẩn bị",
        OrderStatuses.Shipped => "Đang giao",
        OrderStatuses.Delivered => "Đã giao",
        OrderStatuses.Cancelled => "Đã hủy",
        _ => status
    };

    private static string Encode(string? value) => HtmlEncoder.Default.Encode(value ?? string.Empty);
}

public sealed record OrderNotificationResult(bool CustomerNotified, bool StaffNotified);
