using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;

namespace WebApplication1.Data;

public static class DatabaseSeeder
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        using var scope = services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await database.Database.EnsureCreatedAsync();
        await EnsureUserProfileSchemaAsync(database);
        await EnsureOrderReturnSchemaAsync(database);
        await EnsureSupportChatSchemaAsync(database);
        await EnsureProductReviewSchemaAsync(database);
        await EnsureFinancialSchemaAsync(database);

        foreach (var roleName in AppRoles.All)
        {
            if (!await roles.RoleExistsAsync(roleName))
            {
                var result = await roles.CreateAsync(new IdentityRole(roleName));
                EnsureSuccess(result, $"Could not create role {roleName}.");
            }
        }

        var catalog = new[]
            {
            new Product { Slug = "look-01", Name = "Áo khoác dáng mềm", Category = "women", Price = 1890000, ImageId = "photo-1539109136881-3be0616acf4b", Description = "Phom dáng thanh lịch, chất liệu nhẹ cho những ngày chuyển mùa.", SizesCsv = "S,M,L", Color = "Nâu hạt dẻ", Stock = 18 },
            new Product { Slug = "look-02", Name = "Sơ mi cotton cổ điển", Category = "men", Price = 890000, ImageId = "photo-1598033129183-c4f50c736f10", Description = "Cotton thoáng mát cùng đường cắt gọn gàng, dễ mặc mỗi ngày.", SizesCsv = "S,M,L,XL", Color = "Trắng ngà", Stock = 24 },
            new Product { Slug = "look-03", Name = "Túi đeo vai dáng cong", Category = "accessories", Price = 1250000, ImageId = "photo-1584917865442-de89df76afd3", Description = "Một điểm nhấn vừa đủ cho những bản phối tối giản.", SizesCsv = "ONE SIZE", Color = "Đen", Stock = 12 },
            new Product { Slug = "look-04", Name = "Đầm midi nếp gấp", Category = "women", Price = 1590000, ImageId = "photo-1490481651871-ab68de25d43d", Description = "Chuyển động mềm mại, phù hợp từ buổi hẹn cà phê đến tối cuối tuần.", SizesCsv = "S,M,L", Color = "Be cát", Stock = 16 },
            new Product { Slug = "look-05", Name = "Áo thun heavyweight", Category = "men", Price = 590000, ImageId = "photo-1521572163474-6864f9cf17ab", Description = "Chất vải đứng phom, một món cơ bản có thể mặc theo nhiều cách.", SizesCsv = "S,M,L,XL", Color = "Xám đá", Stock = 30 },
            new Product { Slug = "look-06", Name = "Khăn lụa họa tiết", Category = "accessories", Price = 490000, ImageId = "photo-1601924994987-69e26d50dc26", Description = "Họa tiết nghệ thuật để biến tấu cùng cổ áo, túi xách hoặc mái tóc.", SizesCsv = "ONE SIZE", Color = "Nhiều màu", Stock = 20 },
            new Product { Slug = "look-07", Name = "Blazer linen phom rộng", Category = "women", Price = 2190000, ImageId = "photo-1483985988355-763728e1935b", Description = "Lớp linen nhẹ với đường vai tự nhiên cho phong cách thanh lịch hiện đại.", SizesCsv = "S,M,L", Color = "Kem linen", Stock = 14 },
            new Product { Slug = "look-08", Name = "Quần ống rộng xếp ly", Category = "women", Price = 1190000, ImageId = "photo-1496747611176-843222e1e57c", Description = "Cạp cao và ống suông tạo chuyển động mềm mại suốt ngày dài.", SizesCsv = "S,M,L,XL", Color = "Đen tuyền", Stock = 19 },
            new Product { Slug = "look-09", Name = "Áo len cổ thuyền", Category = "women", Price = 990000, ImageId = "photo-1515886657613-9f3515b0c78f", Description = "Dệt kim mịn, cổ thuyền nữ tính, dễ kết hợp cùng chân váy hoặc denim.", SizesCsv = "S,M,L", Color = "Đỏ rượu", Stock = 11 },
            new Product { Slug = "look-10", Name = "Chân váy midi chữ A", Category = "women", Price = 1090000, ImageId = "photo-1512436991641-6745cdb1723f", Description = "Phom chữ A cổ điển với chất vải rủ nhẹ, phù hợp nhiều dịp.", SizesCsv = "S,M,L", Color = "Nâu cacao", Stock = 17 },
            new Product { Slug = "look-11", Name = "Đầm sơ mi cotton", Category = "women", Price = 1390000, ImageId = "photo-1529139574466-a303027c1d8b", Description = "Thiết kế sơ mi thoải mái có thể mặc riêng hoặc phối cùng thắt lưng.", SizesCsv = "S,M,L", Color = "Xanh olive", Stock = 13 },
            new Product { Slug = "look-12", Name = "Áo khoác denim cổ điển", Category = "men", Price = 1490000, ImageId = "photo-1523381210434-271e8be1f52b", Description = "Denim bền đẹp, phom vừa vặn cho những bản phối thường ngày.", SizesCsv = "M,L,XL", Color = "Xanh denim", Stock = 21 },
            new Product { Slug = "look-13", Name = "Quần chino tapered", Category = "men", Price = 990000, ImageId = "photo-1516826957135-700dedea698c", Description = "Ống tapered gọn gàng, chất vải co giãn nhẹ cho ngày năng động.", SizesCsv = "S,M,L,XL", Color = "Be sáng", Stock = 25 },
            new Product { Slug = "look-14", Name = "Áo polo dệt piqué", Category = "men", Price = 690000, ImageId = "photo-1618354691373-d851c5c3a990", Description = "Bề mặt piqué thoáng khí cùng cổ áo chỉn chu, dễ mặc từ công sở đến cuối tuần.", SizesCsv = "S,M,L,XL", Color = "Xanh navy", Stock = 27 },
            new Product { Slug = "look-15", Name = "Áo sơ mi linen tay ngắn", Category = "men", Price = 790000, ImageId = "photo-1603252109303-2751441dd157", Description = "Linen thoáng mát và phom relaxed cho những ngày nắng.", SizesCsv = "S,M,L,XL", Color = "Xanh nhạt", Stock = 18 },
            new Product { Slug = "look-16", Name = "Túi tote canvas", Category = "accessories", Price = 390000, ImageId = "photo-1544816155-12df9643f363", Description = "Túi canvas rộng rãi, tiện dụng cho lịch trình hằng ngày.", SizesCsv = "ONE SIZE", Color = "Canvas tự nhiên", Stock = 32 },
            new Product { Slug = "look-17", Name = "Thắt lưng da tối giản", Category = "accessories", Price = 650000, ImageId = "photo-1624222247344-550fb8c8a9f7", Description = "Phụ kiện da với khóa kim loại tinh giản, hoàn thiện bản phối gọn gàng.", SizesCsv = "S,M,L", Color = "Nâu sẫm", Stock = 15 },
            new Product { Slug = "look-18", Name = "Mũ lưỡi trai cotton", Category = "accessories", Price = 450000, ImageId = "photo-1588850561407-ed78c282e89b", Description = "Cotton mềm nhẹ, điểm nhấn năng động cho những ngày dạo phố.", SizesCsv = "ONE SIZE", Color = "Trắng ngà", Stock = 23 }
        }.Concat(CreateAdditionalProducts()).ToArray();
        var existingSlugs = await database.Products.Select(product => product.Slug).ToHashSetAsync();
        var missingProducts = catalog.Where(product => !existingSlugs.Contains(product.Slug)).ToArray();
        if (missingProducts.Length > 0)
        {
            database.Products.AddRange(missingProducts);
            await database.SaveChangesAsync();
        }

        var adminEmail = configuration["AdminBootstrap:Email"];
        var adminPassword = configuration["AdminBootstrap:Password"];
        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogWarning("Admin bootstrap is not configured. Set AdminBootstrap__Email and AdminBootstrap__Password to create the first administrator account.");
            return;
        }

        var admin = await users.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                DisplayName = "Quản trị viên"
            };
            var createResult = await users.CreateAsync(admin, adminPassword);
            EnsureSuccess(createResult, "Could not create bootstrap administrator. Check the email and password requirements.");
        }
        else if (!await users.IsInRoleAsync(admin, AppRoles.Admin))
        {
            throw new InvalidOperationException("The configured bootstrap email already belongs to a non-administrator account. Choose a new, unused AdminBootstrap__Email; existing customer accounts are never promoted during startup.");
        }

        if (!await users.IsInRoleAsync(admin, AppRoles.Admin))
        {
            var roleResult = await users.AddToRoleAsync(admin, AppRoles.Admin);
            EnsureSuccess(roleResult, "Could not assign the administrator role to the bootstrap account.");
        }
    }

    private static async Task EnsureUserProfileSchemaAsync(ApplicationDbContext database)
    {
        var connection = database.Database.GetDbConnection();
        await database.Database.OpenConnectionAsync();
        try
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var command = connection.CreateCommand();
            command.CommandText = """PRAGMA table_info("AspNetUsers");""";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(1));
            }

            await reader.DisposeAsync();
            if (!columns.Contains("AvatarImageId"))
            {
                await database.Database.ExecuteSqlRawAsync("""ALTER TABLE "AspNetUsers" ADD COLUMN "AvatarImageId" TEXT NULL;""");
            }

            if (!columns.Contains("PhoneNumberIsPublic"))
            {
                await database.Database.ExecuteSqlRawAsync("""ALTER TABLE "AspNetUsers" ADD COLUMN "PhoneNumberIsPublic" INTEGER NOT NULL DEFAULT 0;""");
            }

            if (!columns.Contains("Gender"))
            {
                await database.Database.ExecuteSqlRawAsync("""ALTER TABLE "AspNetUsers" ADD COLUMN "Gender" TEXT NULL;""");
            }

            if (!columns.Contains("Address"))
            {
                await database.Database.ExecuteSqlRawAsync("""ALTER TABLE "AspNetUsers" ADD COLUMN "Address" TEXT NULL;""");
            }
        }
        finally
        {
            await database.Database.CloseConnectionAsync();
        }
    }

    private static async Task EnsureOrderReturnSchemaAsync(ApplicationDbContext database)
    {
        var connection = database.Database.GetDbConnection();
        await database.Database.OpenConnectionAsync();
        try
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA table_info(\"Orders\");";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(1));
            }

            await reader.DisposeAsync();
            if (!columns.Contains("ReceivedAtUtc"))
            {
                await database.Database.ExecuteSqlRawAsync("ALTER TABLE \"Orders\" ADD COLUMN \"ReceivedAtUtc\" TEXT NULL;");
            }
            if (!columns.Contains("ReturnRequestedAtUtc"))
            {
                await database.Database.ExecuteSqlRawAsync("ALTER TABLE \"Orders\" ADD COLUMN \"ReturnRequestedAtUtc\" TEXT NULL;");
            }
            if (!columns.Contains("ReturnReason"))
            {
                await database.Database.ExecuteSqlRawAsync("ALTER TABLE \"Orders\" ADD COLUMN \"ReturnReason\" TEXT NULL;");
            }
            if (!columns.Contains("ReturnVideoId"))
            {
                await database.Database.ExecuteSqlRawAsync("ALTER TABLE \"Orders\" ADD COLUMN \"ReturnVideoId\" TEXT NULL;");
            }
        }
        finally
        {
            await database.Database.CloseConnectionAsync();
        }
    }

    private static async Task EnsureProductReviewSchemaAsync(ApplicationDbContext database)
    {
        await database.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "ProductReviews" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_ProductReviews" PRIMARY KEY AUTOINCREMENT,
                "CustomerOrderId" INTEGER NOT NULL,
                "ProductSlug" TEXT NOT NULL,
                "Rating" INTEGER NOT NULL,
                "Comment" TEXT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                CONSTRAINT "CK_ProductReviews_Rating" CHECK ("Rating" BETWEEN 1 AND 5),
                CONSTRAINT "FK_ProductReviews_Orders_CustomerOrderId"
                    FOREIGN KEY ("CustomerOrderId") REFERENCES "Orders" ("Id") ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_ProductReviews_CustomerOrderId_ProductSlug"
                ON "ProductReviews" ("CustomerOrderId", "ProductSlug");
            CREATE INDEX IF NOT EXISTS "IX_ProductReviews_ProductSlug_CreatedAtUtc"
                ON "ProductReviews" ("ProductSlug", "CreatedAtUtc");
            """);

        var connection = database.Database.GetDbConnection();
        await database.Database.OpenConnectionAsync();
        try
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA table_info(\"ProductReviews\");";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(1));
            }

            await reader.DisposeAsync();
            if (!columns.Contains("StaffReply"))
            {
                await database.Database.ExecuteSqlRawAsync("ALTER TABLE \"ProductReviews\" ADD COLUMN \"StaffReply\" TEXT NULL;");
            }

            if (!columns.Contains("StaffReplyBy"))
            {
                await database.Database.ExecuteSqlRawAsync("ALTER TABLE \"ProductReviews\" ADD COLUMN \"StaffReplyBy\" TEXT NULL;");
            }

            if (!columns.Contains("StaffRepliedAtUtc"))
            {
                await database.Database.ExecuteSqlRawAsync("ALTER TABLE \"ProductReviews\" ADD COLUMN \"StaffRepliedAtUtc\" TEXT NULL;");
            }
        }
        finally
        {
            await database.Database.CloseConnectionAsync();
        }
    }

    private static async Task EnsureFinancialSchemaAsync(ApplicationDbContext database)
    {
        await database.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "StockReceipts" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_StockReceipts" PRIMARY KEY AUTOINCREMENT,
                "ProductSlug" TEXT NOT NULL,
                "ProductName" TEXT NOT NULL,
                "Quantity" INTEGER NOT NULL,
                "UnitCost" TEXT NOT NULL,
                "Supplier" TEXT NOT NULL,
                "Reference" TEXT NOT NULL,
                "ReceivedOn" TEXT NOT NULL,
                "Notes" TEXT NOT NULL,
                "RecordedBy" TEXT NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS "OperatingExpenses" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_OperatingExpenses" PRIMARY KEY AUTOINCREMENT,
                "Category" TEXT NOT NULL,
                "Description" TEXT NOT NULL,
                "Amount" TEXT NOT NULL,
                "Supplier" TEXT NOT NULL,
                "SpentOn" TEXT NOT NULL,
                "Notes" TEXT NOT NULL,
                "RecordedBy" TEXT NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL
            );
            """);
    }

    private static async Task EnsureSupportChatSchemaAsync(ApplicationDbContext database)
    {
        await database.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "SupportConversations" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_SupportConversations" PRIMARY KEY AUTOINCREMENT,
                "CustomerUserId" TEXT NOT NULL,
                "ClientSessionId" TEXT NOT NULL DEFAULT '',
                "AssignedEmployeeUserId" TEXT NULL,
                "Status" TEXT NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "UpdatedAtUtc" TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS "SupportMessages" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_SupportMessages" PRIMARY KEY AUTOINCREMENT,
                "ConversationId" INTEGER NOT NULL,
                "SenderUserId" TEXT NOT NULL,
                "SenderName" TEXT NOT NULL,
                "SenderRole" TEXT NOT NULL,
                "Content" TEXT NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                CONSTRAINT "FK_SupportMessages_SupportConversations_ConversationId"
                    FOREIGN KEY ("ConversationId") REFERENCES "SupportConversations" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_SupportConversations_AssignedEmployeeUserId_Status"
                ON "SupportConversations" ("AssignedEmployeeUserId", "Status");
            CREATE INDEX IF NOT EXISTS "IX_SupportMessages_ConversationId_Id"
                ON "SupportMessages" ("ConversationId", "Id");
            """);

        var connection = database.Database.GetDbConnection();
        await database.Database.OpenConnectionAsync();
        try
        {
            var hasClientSessionId = false;
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA table_info(\"SupportConversations\");";
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (reader.GetString(1) == "ClientSessionId")
                    {
                        hasClientSessionId = true;
                        break;
                    }
                }
            }

            if (!hasClientSessionId)
            {
                await database.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE \"SupportConversations\" ADD COLUMN \"ClientSessionId\" TEXT NOT NULL DEFAULT ''; ");
            }

            await database.Database.ExecuteSqlRawAsync("""
                CREATE INDEX IF NOT EXISTS "IX_SupportConversations_CustomerUserId_ClientSessionId_Status"
                    ON "SupportConversations" ("CustomerUserId", "ClientSessionId", "Status");
                """);
        }
        finally
        {
            await database.Database.CloseConnectionAsync();
        }
    }

    private static IEnumerable<Product> CreateAdditionalProducts()
    {
        var namesByCategory = new (string Category, string[] Names)[]
        {
            ("women", [
                "Áo blouse cổ nơ", "Áo cardigan dệt gân", "Áo tank top cổ vuông", "Áo kiểu tay phồng", "Áo gile linen",
                "Áo sơ mi lụa mềm", "Áo thun cổ tròn nữ", "Áo khoác trench ngắn", "Áo khoác tweed thanh lịch", "Áo len cổ lọ mỏng",
                "Quần jeans ống đứng nữ", "Quần culottes linen", "Quần short cạp cao", "Quần jogger cotton nữ", "Quần tây cạp liền",
                "Chân váy xếp ly ngắn", "Chân váy denim dài", "Chân váy linen quấn", "Đầm hoa tay lỡ", "Đầm maxi hai dây",
                "Đầm dự tiệc cổ vuông", "Đầm len ôm nhẹ", "Jumpsuit ống suông", "Áo khoác bomber nữ", "Áo gió dáng ngắn nữ",
                "Áo cổ lọ tay dài", "Áo ren tay dài", "Áo peplum tối giản"]),
            ("men", [
                "Áo thun cổ tim nam", "Áo sơ mi oxford", "Áo sơ mi kẻ sọc nam", "Áo khoác bomber nam", "Áo cardigan nam",
                "Áo len cổ tròn nam", "Áo hoodie cotton", "Áo khoác gió nam", "Áo vest một nút", "Áo denim overshirt",
                "Quần jeans slim nam", "Quần jeans straight nam", "Quần tây slim fit", "Quần short kaki nam", "Quần jogger thể thao nam",
                "Quần linen ống suông nam", "Áo thun graphic nam", "Áo polo tay dài", "Áo sơ mi chambray", "Áo khoác da tối giản",
                "Áo gile len nam", "Áo khoác coach jacket", "Áo thun cổ henley", "Bộ đồ nỉ thể thao", "Áo sơ mi cổ trụ nam",
                "Quần cargo túi hộp", "Áo khoác field jacket"]),
            ("accessories", [
                "Túi đeo chéo mini", "Túi kẹp nách da", "Balo canvas tối giản", "Ví da gập đôi", "Ví đựng thẻ mỏng",
                "Khăn choàng len mềm", "Khăn bandana cotton", "Mũ bucket vải mềm", "Mũ len gân", "Nón fedora cổ điển",
                "Tất cotton cổ ngắn", "Tất cổ cao họa tiết", "Kính mát gọng vuông", "Kính mát dáng oval", "Dây chuyền mặt nhỏ",
                "Vòng tay kim loại", "Bông tai vòng nhỏ", "Bông tai dáng dài", "Nhẫn bạc tối giản", "Đồng hồ dây da",
                "Đồng hồ dây kim loại", "Túi đeo vai mini", "Túi clutch dự tiệc", "Dây lưng dệt casual", "Dây lưng da bản nhỏ",
                "Cà vạt lụa trơn", "Cà vạt họa tiết cổ điển"])
        };
        var colors = new[] { "Đen", "Trắng ngà", "Be cát", "Nâu cacao", "Xanh navy", "Xanh olive", "Xám đá", "Đỏ rượu", "Kem", "Xanh nhạt" };
        var imageIds = new[]
        {
            "photo-1539109136881-3be0616acf4b", "photo-1598033129183-c4f50c736f10", "photo-1584917865442-de89df76afd3",
            "photo-1490481651871-ab68de25d43d", "photo-1521572163474-6864f9cf17ab", "photo-1601924994987-69e26d50dc26",
            "photo-1483985988355-763728e1935b", "photo-1496747611176-843222e1e57c", "photo-1515886657613-9f3515b0c78f",
            "photo-1512436991641-6745cdb1723f", "photo-1529139574466-a303027c1d8b", "photo-1523381210434-271e8be1f52b",
            "photo-1516826957135-700dedea698c", "photo-1618354691373-d851c5c3a990", "photo-1603252109303-2751441dd157",
            "photo-1544816155-12df9643f363", "photo-1624222247344-550fb8c8a9f7", "photo-1588850561407-ed78c282e89b"
        };
        var index = 18;
        foreach (var (category, names) in namesByCategory)
        {
            foreach (var name in names)
            {
                var color = colors[index % colors.Length];
                var isAccessory = category == "accessories";
                var sizes = isAccessory ? "ONE SIZE" : category == "women" ? "XS,S,M,L,XL" : "S,M,L,XL,XXL";
                var basePrice = category switch { "women" => 590000, "men" => 490000, _ => 290000 };
                yield return new Product
                {
                    Slug = $"look-{index + 1:D2}",
                    Name = name,
                    Category = category,
                    Price = basePrice + (index % 12) * 100000,
                    ImageId = imageIds[index % imageIds.Length],
                    Description = $"Thiết kế {name.ToLowerInvariant()} với gam màu {color.ToLowerInvariant()}, dễ phối cùng các món đồ cơ bản trong tủ quần áo.",
                    SizesCsv = sizes,
                    Color = color,
                    Stock = 8 + index % 23
                };
                index++;
            }
        }
    }

    private static void EnsureSuccess(IdentityResult result, string message)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"{message} {string.Join("; ", result.Errors.Select(error => error.Description))}");
        }
    }
}
