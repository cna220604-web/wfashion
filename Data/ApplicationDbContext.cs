using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;

namespace WebApplication1.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<CustomerOrder> Orders => Set<CustomerOrder>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();
    public DbSet<StockReceipt> StockReceipts => Set<StockReceipt>();
    public DbSet<OperatingExpense> OperatingExpenses => Set<OperatingExpense>();
    public DbSet<SupportConversation> SupportConversations => Set<SupportConversation>();
    public DbSet<SupportMessage> SupportMessages => Set<SupportMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Product>().Property(product => product.Price).HasPrecision(12, 2);
        builder.Entity<StockReceipt>().ToTable("StockReceipts");
        builder.Entity<StockReceipt>().Property(receipt => receipt.UnitCost).HasPrecision(12, 2);
        builder.Entity<StockReceipt>().Property(receipt => receipt.ProductSlug).HasMaxLength(20);
        builder.Entity<StockReceipt>().Property(receipt => receipt.ProductName).HasMaxLength(120);
        builder.Entity<StockReceipt>().Property(receipt => receipt.Supplier).HasMaxLength(120);
        builder.Entity<StockReceipt>().Property(receipt => receipt.Reference).HasMaxLength(80);
        builder.Entity<StockReceipt>().Property(receipt => receipt.Notes).HasMaxLength(500);
        builder.Entity<StockReceipt>().Property(receipt => receipt.RecordedBy).HasMaxLength(450);
        builder.Entity<OperatingExpense>().ToTable("OperatingExpenses");
        builder.Entity<OperatingExpense>().Property(expense => expense.Amount).HasPrecision(12, 2);
        builder.Entity<OperatingExpense>().Property(expense => expense.Category).HasMaxLength(80);
        builder.Entity<OperatingExpense>().Property(expense => expense.Description).HasMaxLength(160);
        builder.Entity<OperatingExpense>().Property(expense => expense.Supplier).HasMaxLength(120);
        builder.Entity<OperatingExpense>().Property(expense => expense.Notes).HasMaxLength(500);
        builder.Entity<OperatingExpense>().Property(expense => expense.RecordedBy).HasMaxLength(450);
        builder.Entity<CustomerOrder>().Property(order => order.Subtotal).HasPrecision(12, 2);
        builder.Entity<CustomerOrder>().Property(order => order.ShippingFee).HasPrecision(12, 2);
        builder.Entity<CustomerOrder>().Property(order => order.Total).HasPrecision(12, 2);
        builder.Entity<OrderItem>().Property(item => item.UnitPrice).HasPrecision(12, 2);
        builder.Entity<CustomerOrder>().HasMany(order => order.Items)
            .WithOne(item => item.Order)
            .HasForeignKey(item => item.CustomerOrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<ProductReview>().ToTable("ProductReviews", table =>
            table.HasCheckConstraint("CK_ProductReviews_Rating", "[Rating] BETWEEN 1 AND 5"));
        builder.Entity<ProductReview>().Property(review => review.ProductSlug).HasMaxLength(80);
        builder.Entity<ProductReview>().Property(review => review.Comment).HasMaxLength(1000);
        builder.Entity<ProductReview>().Property(review => review.StaffReply).HasMaxLength(1000);
        builder.Entity<ProductReview>().Property(review => review.StaffReplyBy).HasMaxLength(100);
        builder.Entity<ProductReview>().HasOne(review => review.Order)
            .WithMany()
            .HasForeignKey(review => review.CustomerOrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<ProductReview>().HasIndex(review => new { review.CustomerOrderId, review.ProductSlug }).IsUnique();
        builder.Entity<ProductReview>().HasIndex(review => new { review.ProductSlug, review.CreatedAtUtc });
        builder.Entity<CustomerOrder>().HasIndex(order => new { order.UserId, order.CreatedAtUtc });
        builder.Entity<SupportConversation>().HasIndex(conversation => new { conversation.CustomerUserId, conversation.ClientSessionId, conversation.Status });
        builder.Entity<SupportConversation>().HasIndex(conversation => new { conversation.AssignedEmployeeUserId, conversation.Status });
        builder.Entity<SupportConversation>().HasMany(conversation => conversation.Messages)
            .WithOne(message => message.Conversation)
            .HasForeignKey(message => message.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<SupportMessage>().HasIndex(message => new { message.ConversationId, message.Id });
    }
}
