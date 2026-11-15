using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;

namespace WebApplication1.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductCategoryRecord> ProductCategories => Set<ProductCategoryRecord>();
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

        if (Database.IsSqlServer())
        {
            builder.Entity<ProductCategoryRecord>().ToTable("Categories");
            builder.Entity<ProductCategoryRecord>().HasKey(category => category.CategoryId);
            builder.Entity<ProductCategoryRecord>().Property(category => category.CategoryId).HasColumnName("CategoryID");
            builder.Entity<ProductCategoryRecord>().Property(category => category.CategoryName).HasColumnName("CategoryName").HasMaxLength(100);

            builder.Entity<Product>().ToTable("Products");
            builder.Entity<Product>().HasKey(product => product.ProductId);
            builder.Entity<Product>().Property(product => product.ProductId).HasColumnName("ProductID").ValueGeneratedOnAdd();
            builder.Entity<Product>().Property(product => product.CategoryId).HasColumnName("CategoryID");
            builder.Entity<Product>().Property(product => product.Slug).HasColumnName("ProductCode").HasMaxLength(20).IsUnicode(false);
            builder.Entity<Product>().Property(product => product.Name).HasColumnName("ProductName").HasMaxLength(200);
            builder.Entity<Product>().Property(product => product.Category).HasColumnName("StoreCategory").HasMaxLength(30);
            builder.Entity<Product>().Property(product => product.Price).HasColumnName("UnitPrice").HasPrecision(12, 2);
            builder.Entity<Product>().Property(product => product.ImageId).HasMaxLength(100);
            builder.Entity<Product>().Property(product => product.Description).HasMaxLength(500);
            builder.Entity<Product>().Property(product => product.SizesCsv).HasColumnName("SizesCsv").HasMaxLength(100);
            builder.Entity<Product>().Property(product => product.Color).HasMaxLength(50);
            builder.Entity<Product>().Property(product => product.Stock).HasColumnName("StockQuantity");
            builder.Entity<Product>().Property(product => product.CreatedAtUtc).HasColumnName("CreatedAt");
            builder.Entity<Product>().HasIndex(product => product.Slug).IsUnique().HasDatabaseName("UQ_Products_ProductCode");
            builder.Entity<Product>().HasOne(product => product.ProductCategory)
                .WithMany(category => category.Products)
                .HasForeignKey(product => product.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        }
        else
        {
            builder.Ignore<ProductCategoryRecord>();
            builder.Entity<Product>().Ignore(product => product.ProductId);
            builder.Entity<Product>().Ignore(product => product.CategoryId);
            builder.Entity<Product>().Ignore(product => product.ProductCategory);
        }

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
