using freshcrumbs.CRM.domain.entities;
using freshcrumbs.CRM.infrastructure.services;
using Microsoft.EntityFrameworkCore;

namespace freshcrumbs.CRM.api.Services
{
    public static class TenantDemoDataSeeder
    {
        private const string Marker = "DEMO-";

        private static readonly string[] FirstNames =
        {
            "Maria", "Jose", "Ana", "Juan", "Angela", "Mark", "Joyce", "Paolo", "Katrina", "Miguel",
            "Bianca", "Carlo", "Denise", "Rafael", "Sofia", "Gabriel", "Isabel", "Andres", "Camille", "Rico"
        };

        private static readonly string[] LastNames =
        {
            "Santos", "Reyes", "Cruz", "Bautista", "Ocampo", "Garcia", "Mendoza", "Torres", "Flores", "Ramos"
        };

        private static readonly (string Name, string Category, decimal Price, string Description)[] ProductSeed =
        {
            ("Chocolate Fudge Cake", "Cakes", 650m, "Rich chocolate cake with fudge frosting"),
            ("Ube Cheese Cake", "Cakes", 720m, "Ube cake topped with cream cheese"),
            ("Red Velvet Cupcake", "Cupcakes", 85m, "Red velvet with cream cheese frosting"),
            ("Vanilla Cupcake", "Cupcakes", 70m, "Classic vanilla cupcake"),
            ("Ensaymada", "Pastries", 55m, "Soft brioche topped with butter and cheese"),
            ("Croissant", "Pastries", 95m, "Buttery flaky croissant"),
            ("Chocolate Chip Cookie", "Cookies", 45m, "Chewy cookie with chocolate chips"),
            ("Oatmeal Raisin Cookie", "Cookies", 40m, "Oatmeal cookie with raisins"),
            ("Pandesal Pack", "Bread", 60m, "Pack of ten fresh pandesal"),
            ("Sourdough Loaf", "Bread", 180m, "Naturally leavened sourdough"),
            ("Leche Flan", "Desserts", 150m, "Creamy caramel custard"),
            ("Iced Coffee", "Beverages", 110m, "Cold brew with milk")
        };

        private static readonly string[] PaymentMethods = { "Cash", "GCash", "Card", "Bank Transfer" };

        private static readonly string[] FeedbackTypes = { "Complaint", "Feedback", "Suggestion" };

        private static readonly string[] FeedbackCategories =
        {
            "Customer Service", "Product Quality", "Product Availability", "Orders", "Pricing and Payments", "Packaging"
        };

        private static readonly string[] FeedbackComments =
        {
            "The staff were friendly and helpful.",
            "The pastry was a bit dry today.",
            "My favorite item was sold out again.",
            "Order was ready on time, thank you.",
            "Please offer more payment options.",
            "The box was damaged when I got home.",
            "Loved the new cake flavor.",
            "Prices have gone up recently.",
            "Please extend the opening hours.",
            "Great value for the quality."
        };

        private static readonly string[] InquiryTypes = { "Product", "Order", "Payment", "Promotion", "Other" };

        private static readonly string[] InquirySources = { "Phone Call", "Email", "Facebook", "Walk-in", "Other" };

        private static readonly string[] InquirySubjects =
        {
            "Custom cake availability",
            "Order pickup schedule",
            "Accepted payment methods",
            "Current promotions",
            "Bulk order pricing",
            "Ingredient and allergen details",
            "Delivery options",
            "Birthday cake lead time"
        };

        public static async Task SeedAsync(IServiceProvider services, int companyId)
        {
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("TenantDemoDataSeeder");

            try
            {
                var factory = services.GetRequiredService<ITenantDbContextFactory>();
                await using var db = await factory.CreateAsync(companyId);

                if (await db.Customers.AnyAsync(x => x.CustomerCode.StartsWith(Marker)))
                {
                    logger.LogInformation("Demo data already exists for company {CompanyId}. Nothing was seeded.", companyId);
                    return;
                }

                var rng = new Random(2026);
                var now = DateTime.UtcNow;

                var products = ProductSeed
                    .Select((p, i) => new Product
                    {
                        ProductCode = $"{Marker}P{i + 1:000}",
                        ProductName = p.Name,
                        Category = p.Category,
                        Description = p.Description,
                        Price = p.Price,
                        Quantity = 400,
                        ReorderLevel = 20,
                        Status = "Active",
                        CreatedAt = now.AddDays(-120)
                    })
                    .ToList();

                var customers = new List<Customer>();
                for (var i = 0; i < 60; i++)
                {
                    var first = FirstNames[i % FirstNames.Length];
                    var last = LastNames[(i / FirstNames.Length + i) % LastNames.Length];

                    customers.Add(new Customer
                    {
                        CustomerCode = $"{Marker}C{i + 1:000}",
                        FirstName = first,
                        LastName = last,
                        Email = $"{first}.{last}{i + 1}@example.com".ToLowerInvariant(),
                        ContactNo = "09" + rng.Next(100000000, 999999999),
                        Address = $"{rng.Next(1, 200)} Rizal Street, Davao City",
                        LoyaltyPoints = 0,
                        Status = i % 15 == 14 ? "Inactive" : "Active",
                        CreatedAt = now.AddDays(-rng.Next(30, 120))
                    });
                }

                var verifiedCategories = new Dictionary<Customer, HashSet<string>>();
                var eligibilities = new List<CustomerDiscountEligibility>();
                for (var i = 0; i < customers.Count; i += 5)
                {
                    var category = (i / 5) % 2 == 0 ? "Senior Citizen" : "PWD";
                    var verified = (i / 5) % 3 != 2;

                    eligibilities.Add(new CustomerDiscountEligibility
                    {
                        Customer = customers[i],
                        Category = category,
                        IdNumber = $"{(category == "PWD" ? "PWD" : "SC")}-{100000 + i}",
                        VerificationStatus = verified ? "Verified" : "Pending Verification"
                    });

                    if (verified)
                    {
                        verifiedCategories[customers[i]] = new HashSet<string> { category };
                    }
                }

                var promotions = new List<Promotion>
                {
                    new Promotion
                    {
                        PromotionName = "Weekend Treat 10% Off",
                        Description = "10% off on purchases of 300 or more",
                        DiscountType = "Percentage",
                        DiscountValue = 10m,
                        MinimumPurchase = 300m,
                        RequiredLoyaltyPoints = 0,
                        StartDate = now.Date.AddDays(-120),
                        EndDate = now.Date.AddDays(60),
                        Status = "Active"
                    },
                    new Promotion
                    {
                        PromotionName = "Fifty Peso Voucher",
                        Description = "50 pesos off on purchases of 250 or more",
                        DiscountType = "Fixed Amount",
                        DiscountValue = 50m,
                        MinimumPurchase = 250m,
                        RequiredLoyaltyPoints = 0,
                        StartDate = now.Date.AddDays(-120),
                        EndDate = now.Date.AddDays(60),
                        Status = "Active"
                    },
                    new Promotion
                    {
                        PromotionName = "Loyalty Redemption 100 Off",
                        Description = "Redeem 20 points for 100 pesos off on purchases of 500 or more",
                        DiscountType = "Fixed Amount",
                        DiscountValue = 100m,
                        MinimumPurchase = 500m,
                        RequiredLoyaltyPoints = 20,
                        StartDate = now.Date.AddDays(-120),
                        EndDate = now.Date.AddDays(60),
                        Status = "Active"
                    },
                    new Promotion
                    {
                        PromotionName = "Senior Citizen Treat",
                        Description = "20% off for verified senior citizens",
                        DiscountType = "Percentage",
                        DiscountValue = 20m,
                        MinimumPurchase = 0m,
                        RequiredLoyaltyPoints = 0,
                        StartDate = now.Date.AddDays(-120),
                        EndDate = now.Date.AddDays(60),
                        Status = "Active",
                        EligibilityCategory = "Senior Citizen"
                    },
                    new Promotion
                    {
                        PromotionName = "Anniversary Sale",
                        Description = "Past promotion kept for history",
                        DiscountType = "Percentage",
                        DiscountValue = 15m,
                        MinimumPurchase = 200m,
                        RequiredLoyaltyPoints = 0,
                        StartDate = now.Date.AddDays(-200),
                        EndDate = now.Date.AddDays(-100),
                        Status = "Inactive"
                    }
                };

                db.Products.AddRange(products);
                db.Customers.AddRange(customers);
                db.CustomerDiscountEligibilities.AddRange(eligibilities);
                db.Promotions.AddRange(promotions);
                await db.SaveChangesAsync();

                var dates = Enumerable.Range(0, 80)
                    .Select(_ => now.Date.AddDays(-rng.Next(1, 90)).AddHours(rng.Next(8, 20)).AddMinutes(rng.Next(0, 60)))
                    .OrderBy(d => d)
                    .ToList();

                foreach (var date in dates)
                {
                    var customer = customers[rng.Next(customers.Count)];
                    var roll = rng.Next(100);
                    var status = roll < 85 ? "Completed" : roll < 93 ? "Cancelled" : "Pending";

                    var sale = new SalesTransaction
                    {
                        Customer = customer,
                        TransactionDate = date,
                        PaymentMethod = PaymentMethods[rng.Next(PaymentMethods.Length)],
                        Status = status
                    };

                    var picked = products.OrderBy(_ => rng.Next()).Take(rng.Next(1, 5)).ToList();
                    foreach (var product in picked)
                    {
                        var quantity = rng.Next(1, 4);
                        sale.TransactionItems.Add(new TransactionItem
                        {
                            Product = product,
                            Quantity = quantity,
                            UnitPrice = product.Price,
                            Subtotal = quantity * product.Price
                        });

                        if (status == "Completed")
                        {
                            product.Quantity -= quantity;
                        }
                    }

                    sale.TotalAmount = sale.TransactionItems.Sum(x => x.Subtotal);

                    Promotion? promotion = null;
                    if (status != "Pending" && rng.Next(100) < 45)
                    {
                        var options = promotions
                            .Where(p => p.Status == "Active"
                                && date >= p.StartDate
                                && date <= p.EndDate
                                && sale.TotalAmount >= p.MinimumPurchase
                                && customer.LoyaltyPoints >= p.RequiredLoyaltyPoints
                                && (string.IsNullOrEmpty(p.EligibilityCategory)
                                    || (verifiedCategories.TryGetValue(customer, out var cats) && cats.Contains(p.EligibilityCategory))))
                            .ToList();

                        if (options.Count > 0)
                        {
                            promotion = options[rng.Next(options.Count)];
                        }
                    }

                    var discount = 0m;
                    if (promotion != null)
                    {
                        discount = promotion.DiscountType == "Percentage"
                            ? sale.TotalAmount * (promotion.DiscountValue / 100m)
                            : promotion.DiscountValue;
                        discount = Math.Round(Math.Min(discount, sale.TotalAmount), 2);
                        sale.Promotion = promotion;
                    }

                    sale.DiscountAmount = discount;
                    sale.CustomerDiscountAmount = 0m;
                    sale.FinalAmount = sale.TotalAmount - discount;

                    if (status != "Pending")
                    {
                        sale.PointsUsed = promotion?.RequiredLoyaltyPoints ?? 0;
                        sale.PointsEarned = (int)Math.Min(Math.Floor(sale.FinalAmount / 10m), 100m);
                    }

                    db.SalesTransactions.Add(sale);

                    if (sale.PointsEarned != 0 || sale.PointsUsed != 0)
                    {
                        db.LoyaltyTransactions.Add(new LoyaltyTransaction
                        {
                            Customer = customer,
                            SalesTransaction = sale,
                            PointsEarned = sale.PointsEarned,
                            PointsUsed = sale.PointsUsed,
                            TransactionType = "Sale",
                            Date = date
                        });

                        if (status == "Completed")
                        {
                            customer.LoyaltyPoints += sale.PointsEarned - sale.PointsUsed;
                        }
                        else
                        {
                            db.LoyaltyTransactions.Add(new LoyaltyTransaction
                            {
                                Customer = customer,
                                SalesTransaction = sale,
                                PointsEarned = sale.PointsUsed,
                                PointsUsed = sale.PointsEarned,
                                TransactionType = "Cancellation",
                                Date = date.AddMinutes(30)
                            });
                        }
                    }
                }

                await db.SaveChangesAsync();

                var statusesFeedback = new[] { "Pending", "Reviewed", "Resolved" };
                for (var i = 0; i < 25; i++)
                {
                    db.Feedbacks.Add(new Feedback
                    {
                        Customer = customers[rng.Next(customers.Count)],
                        Type = FeedbackTypes[rng.Next(FeedbackTypes.Length)],
                        Category = FeedbackCategories[rng.Next(FeedbackCategories.Length)],
                        Comment = FeedbackComments[rng.Next(FeedbackComments.Length)],
                        DateSubmitted = now.AddDays(-rng.Next(1, 60)),
                        Status = statusesFeedback[rng.Next(statusesFeedback.Length)]
                    });
                }

                var statusesInquiry = new[] { "Pending", "In Progress", "Completed" };
                for (var i = 0; i < 25; i++)
                {
                    var inquiryStatus = statusesInquiry[rng.Next(statusesInquiry.Length)];
                    var submitted = now.AddDays(-rng.Next(1, 60));

                    db.Inquiries.Add(new Inquiry
                    {
                        Customer = customers[rng.Next(customers.Count)],
                        Type = InquiryTypes[rng.Next(InquiryTypes.Length)],
                        Source = InquirySources[rng.Next(InquirySources.Length)],
                        Subject = InquirySubjects[rng.Next(InquirySubjects.Length)],
                        Message = "Customer asked for more details about this topic.",
                        DateSubmitted = submitted,
                        Status = inquiryStatus,
                        Response = inquiryStatus == "Completed" ? "Details were provided to the customer." : string.Empty,
                        RespondedBy = inquiryStatus == "Completed" ? "Demo Staff" : string.Empty,
                        RespondedAt = inquiryStatus == "Completed" ? submitted.AddHours(4) : null
                    });
                }

                await db.SaveChangesAsync();

                logger.LogInformation(
                    "Demo data seeded for company {CompanyId}: {Products} products, {Customers} customers, {Promotions} promotions, {Sales} sales.",
                    companyId, products.Count, customers.Count, promotions.Count, dates.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Demo data seeding failed for company {CompanyId}.", companyId);
            }
        }
    }
}
