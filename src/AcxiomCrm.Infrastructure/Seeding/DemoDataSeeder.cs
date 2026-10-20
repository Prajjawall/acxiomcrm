using System.Globalization;
using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Infrastructure.Identity;
using AcxiomCrm.Infrastructure.Options;
using AcxiomCrm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AcxiomCrm.Infrastructure.Seeding;

public sealed class DemoDataSeeder(
    CrmDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IClock clock,
    IHostEnvironment environment,
    IOptions<DatabaseOptions> databaseOptions,
    ILogger<DemoDataSeeder> logger)
{
    public const string AdminPassword = "Admin#2026!Pass";
    public const string ManagerPassword = "Manager#2026!Pass";
    public const string ExecutivePassword = "Exec#2026!Pass";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        SeedGuard.EnsureAllowed(environment, databaseOptions.Value);

        if (!databaseOptions.Value.SeedDemoData)
        {
            return;
        }

        // Idempotency check: if demo manager exists, assume already seeded
        var existingManager = await userManager.FindByEmailAsync("rohan.mehta@acxiomcrm.local");
        if (existingManager != null)
        {
            return;
        }

        logger.LogInformation("Starting demo data seeding...");

        var now = clock.UtcNow;
        var today = clock.Today;

        // 1. Seed Users
        var admin = await EnsureUserAsync("admin@acxiomcrm.local", "System Administrator", Roles.Admin, AdminPassword, null, now);
        var rohan = await EnsureUserAsync("rohan.mehta@acxiomcrm.local", "Rohan Mehta", Roles.Manager, ManagerPassword, null, now);
        var priya = await EnsureUserAsync("priya.nair@acxiomcrm.local", "Priya Nair", Roles.Manager, ManagerPassword, null, now);

        var asha = await EnsureUserAsync("asha.kulkarni@acxiomcrm.local", "Asha Kulkarni", Roles.SalesExecutive, ExecutivePassword, rohan.Id, now);
        var vikram = await EnsureUserAsync("vikram.singh@acxiomcrm.local", "Vikram Singh", Roles.SalesExecutive, ExecutivePassword, rohan.Id, now);
        var neha = await EnsureUserAsync("neha.iyer@acxiomcrm.local", "Neha Iyer", Roles.SalesExecutive, ExecutivePassword, priya.Id, now);
        var arjun = await EnsureUserAsync("arjun.reddy@acxiomcrm.local", "Arjun Reddy", Roles.SalesExecutive, ExecutivePassword, priya.Id, now);

        // 2. Seed Customers (15)
        var customers = new List<Customer>
        {
            Customer.Create("Tata Consultancy Services", "TCS Ltd", "contact@tcs-demo.in", "9820011223", "Bandra Kurla Complex", "Mumbai", "Maharashtra", CustomerStatus.Active, asha.Id, "Key enterprise account"),
            Customer.Create("Infosys Technologies", "Infosys Ltd", "contact@infosys-demo.in", "9845012345", "Electronics City", "Bengaluru", "Karnataka", CustomerStatus.Active, asha.Id, "Global IT consultancy"),
            Customer.Create("Wipro Digital", "Wipro Enterprises", "contact@wipro-demo.in", "9845023456", "Sarjapur Road", "Bengaluru", "Karnataka", CustomerStatus.Active, asha.Id, "Digital transformation leader"),
            Customer.Create("Reliance Retail", "Reliance Industries", "info@relianceretail-demo.in", "9820034567", "Maker Chambers IV, Nariman Point", "Mumbai", "Maharashtra", CustomerStatus.Active, vikram.Id, "Largest retail chain in India"),
            Customer.Create("HDFC Life Insurance", "HDFC Life", "support@hdfclife-demo.in", "9820045678", "Lower Parel", "Mumbai", "Maharashtra", CustomerStatus.Active, vikram.Id, "Financial services client"),
            Customer.Create("Bharti Airtel", "Bharti Enterprises", "sales@airtel-demo.in", "9810056789", "Nelson Mandela Road, Vasant Kunj", "Delhi", "Delhi", CustomerStatus.Active, vikram.Id, "Telecom giant"),
            Customer.Create("Larsen & Toubro", "L&T Group", "contact@lnt-demo.in", "9820067890", "Powai", "Mumbai", "Maharashtra", CustomerStatus.Active, vikram.Id, "Heavy engineering and construction"),
            Customer.Create("Bajaj Finserv", "Bajaj Group", "finance@bajajfinserv-demo.in", "9822078901", "Viman Nagar", "Pune", "Maharashtra", CustomerStatus.Active, neha.Id, "Consumer lending and insurance"),
            Customer.Create("Godrej Consumer Products", "Godrej Industries", "contact@godrej-demo.in", "9820089012", "Pirojshanagar, Vikhroli", "Mumbai", "Maharashtra", CustomerStatus.Active, neha.Id, "FMCG manufacturer"),
            Customer.Create("Asian Paints", "Asian Paints Ltd", "sales@asianpaints-demo.in", "9820090123", "Santacruz East", "Mumbai", "Maharashtra", CustomerStatus.Active, neha.Id, "Decorative paint market leader"),
            Customer.Create("Titan Company", "Tata Group", "corporate@titan-demo.in", "9845034567", "Hosur Road", "Bengaluru", "Karnataka", CustomerStatus.Active, neha.Id, "Watches, jewellery and eyewear"),
            Customer.Create("Sun Pharmaceutical", "Sun Pharma Ltd", "info@sunpharma-demo.in", "9820012344", "Goregaon East", "Mumbai", "Maharashtra", CustomerStatus.Active, arjun.Id, "India's largest pharmaceutical firm"),
            Customer.Create("Adani Ports & SEZ", "Adani Group", "ports@adani-demo.in", "9898023455", "Navrangpura", "Ahmedabad", "Gujarat", CustomerStatus.Active, arjun.Id, "Port infrastructure operations"),
            Customer.Create("ITC Infotech", "ITC Ltd", "reachus@itc-demo.in", "9830034566", "Virginia House, J.L. Nehru Road", "Kolkata", "West Bengal", CustomerStatus.Active, arjun.Id, "Technology services branch of ITC"),
            Customer.Create("Mahindra & Mahindra", "Mahindra Group", "auto@mahindra-demo.in", "9820045677", "Gateway Building, Apollo Bunder", "Mumbai", "Maharashtra", CustomerStatus.Active, arjun.Id, "Automotive & farm equipment")
        };

        dbContext.Customers.AddRange(customers);
        await dbContext.SaveChangesAsync(ct);

        // 3. Seed Leads (25)
        var leads = new List<Lead>
        {
            // New leads
            Lead.Create("Zomato Enterprise", "Zomato Ltd", "corp@zomato-demo.in", "9811122233", LeadSource.Website, LeadPriority.High, 1500000m, asha.Id, "Inbound demo request from website"),
            Lead.Create("Swiggy Solutions", "Bundl Technologies", "b2b@swiggy-demo.in", "9845112233", LeadSource.Referral, LeadPriority.Medium, 800000m, asha.Id, "Referred by Wipro partner"),
            Lead.Create("Razorpay Payments", "Razorpay Software", "sales@razorpay-demo.in", "9845223344", LeadSource.ColdCall, LeadPriority.High, 2500000m, vikram.Id, "Outreach to VP Engineering"),
            Lead.Create("Zerodha Broking", "Zerodha", "info@zerodha-demo.in", "9845334455", LeadSource.Website, LeadPriority.Medium, 1200000m, vikram.Id, "Inbound pricing inquiry"),
            Lead.Create("PolicyBazaar Insurance", "PB Fintech", "leads@policybazaar-demo.in", "9811223344", LeadSource.Campaign, LeadPriority.High, 1800000m, neha.Id, "Webinar campaign registration"),
            Lead.Create("Nykaa Retail", "FSN E-Commerce", "brand@nykaa-demo.in", "9820112233", LeadSource.SocialMedia, LeadPriority.Low, 500000m, arjun.Id, "LinkedIn message inquiry"),

            // Contacted leads
            Lead.Create("Delhivery Logistics", "Delhivery Ltd", "fleet@delhivery-demo.in", "9811334455", LeadSource.Other, LeadPriority.Medium, 950000m, asha.Id, "Intro call held"),
            Lead.Create("BigBasket Groceries", "Supermarket Grocery", "support@bigbasket-demo.in", "9845445566", LeadSource.Website, LeadPriority.High, 1400000m, asha.Id, "Discussed CRM integration requirements"),
            Lead.Create("Urban Company", "UrbanClap Technologies", "vendor@urbancompany-demo.in", "9810112233", LeadSource.Referral, LeadPriority.Medium, 750000m, vikram.Id, "Initial discovery call conducted"),
            Lead.Create("Cred Financial", "Dreamplug Technologies", "partners@cred-demo.in", "9845556677", LeadSource.Campaign, LeadPriority.High, 3000000m, vikram.Id, "High interest in enterprise tier"),
            Lead.Create("Meesho Marketplace", "Fashnear Technologies", "seller@meesho-demo.in", "9845667788", LeadSource.SocialMedia, LeadPriority.Low, 400000m, neha.Id, "Exchanged preliminary emails"),
            Lead.Create("Lenskart Eyewear", "Lenskart Solutions", "stores@lenskart-demo.in", "9810223344", LeadSource.Website, LeadPriority.Medium, 1100000m, arjun.Id, "Call with Retail Operations manager"),

            // Qualified leads
            Lead.Create("PhonePe Payments", "PhonePe Pvt Ltd", "corp@phonepe-demo.in", "9845778899", LeadSource.Other, LeadPriority.High, 4500000m, asha.Id, "Budget approved; RFQ sent"),
            Lead.Create("Pine Labs FinTech", "Pine Labs", "merchant@pinelabs-demo.in", "9818112233", LeadSource.Referral, LeadPriority.High, 3200000m, vikram.Id, "Tech requirements mapped"),
            Lead.Create("Groww Investments", "Nextbillion Technology", "wealth@groww-demo.in", "9845889900", LeadSource.Campaign, LeadPriority.High, 2000000m, neha.Id, "Formal evaluation initiated"),
            Lead.Create("Mamaearth Personal Care", "Honasa Consumer", "trade@mamaearth-demo.in", "9811445566", LeadSource.Website, LeadPriority.Medium, 1350000m, neha.Id, "Scope finalized"),
            Lead.Create("Shiprocket Logistics", "BigFoot Retail", "sales@shiprocket-demo.in", "9810334455", LeadSource.Referral, LeadPriority.Medium, 900000m, arjun.Id, "Security review cleared"),

            // Unqualified leads
            Lead.Create("QuickRepair Services", "QuickRepair Pvt Ltd", "service@quickrepair-demo.in", "9820223344", LeadSource.ColdCall, LeadPriority.Low, 1500000m, asha.Id, "No current CRM budget"),
            Lead.Create("DailyFresh Dairy", "DailyFresh Foods", "dailyfresh@dailyfresh-demo.in", "9822112233", LeadSource.Website, LeadPriority.Low, 200000m, vikram.Id, "Too small for enterprise CRM"),

            // Lost leads
            Lead.Create("CloudNine Health", "Kids Clinic India", "admin@cloudnine-demo.in", "9845990011", LeadSource.Event, LeadPriority.Medium, 850000m, neha.Id, "Selected legacy vendor"),
            Lead.Create("FirstCry Baby", "Brainbees Solutions", "ops@firstcry-demo.in", "9822223344", LeadSource.Referral, LeadPriority.High, 1750000m, arjun.Id, "Postponed project to next fiscal year"),
            Lead.Create("CarDekho Auto", "Girnar Software", "dealer@cardekho-demo.in", "9829011223", LeadSource.Website, LeadPriority.Medium, 650000m, arjun.Id, "Chose in-house tool"),

            // Converted leads
            Lead.Create("Tata Inbound Lead", "TCS", "contact@tcs-demo.in", "9820011223", LeadSource.Website, LeadPriority.High, 4000000m, asha.Id, "Successfully converted to TCS customer"),
            Lead.Create("Infosys Inbound Lead", "Infosys", "contact@infosys-demo.in", "9845012345", LeadSource.Campaign, LeadPriority.High, 7000000m, asha.Id, "Converted after POC demo"),
            Lead.Create("Reliance Outreach Lead", "Reliance", "info@relianceretail-demo.in", "9820034567", LeadSource.Referral, LeadPriority.High, 10000000m, vikram.Id, "Converted after executive pitch")
        };

        // Transition statuses
        // Contacted leads (indices 6..11)
        for (var i = 6; i <= 11; i++)
        {
            leads[i].MoveTo(LeadStatus.Contacted);
        }

        // Qualified leads (indices 12..16)
        for (var i = 12; i <= 16; i++)
        {
            leads[i].MoveTo(LeadStatus.Contacted);
            leads[i].MoveTo(LeadStatus.Qualified);
        }

        // Unqualified leads (indices 17..18)
        for (var i = 17; i <= 18; i++)
        {
            leads[i].MoveTo(LeadStatus.Unqualified);
        }

        // Lost leads (indices 19..21)
        for (var i = 19; i <= 21; i++)
        {
            leads[i].MoveTo(LeadStatus.Contacted);
            leads[i].MoveTo(LeadStatus.Lost);
        }

        // Converted leads (indices 22..24)
        leads[22].MoveTo(LeadStatus.Contacted);
        leads[22].MoveTo(LeadStatus.Qualified);
        leads[22].MarkConverted(customers[0].Id, null, now.AddDays(-30));

        leads[23].MoveTo(LeadStatus.Contacted);
        leads[23].MoveTo(LeadStatus.Qualified);
        leads[23].MarkConverted(customers[1].Id, null, now.AddDays(-20));

        leads[24].MoveTo(LeadStatus.Contacted);
        leads[24].MoveTo(LeadStatus.Qualified);
        leads[24].MarkConverted(customers[3].Id, null, now.AddDays(-15));

        dbContext.Leads.AddRange(leads);
        await dbContext.SaveChangesAsync(ct);

        // 4. Seed Opportunities (20)
        var opportunities = new List<Opportunity>
        {
            // Open opportunities
            Opportunity.Create("TCS Cloud Migration", customers[0].Id, 4500000m, OpportunityStage.Proposal, 40, today.AddDays(45), asha.Id, notes: "Hybrid cloud deployment"),
            Opportunity.Create("Infosys ERP Modernization", customers[1].Id, 8000000m, OpportunityStage.Negotiation, 70, today.AddDays(20), asha.Id, notes: "Finalizing commercial terms"),
            Opportunity.Create("Wipro Analytics Platform", customers[2].Id, 3000000m, OpportunityStage.Qualification, 20, today.AddDays(60), asha.Id, notes: "Initial scoping meeting"),
            Opportunity.Create("Reliance Store Automation", customers[3].Id, 12000000m, OpportunityStage.Proposal, 50, today.AddDays(30), vikram.Id, notes: "Rollout across 500 retail outlets"),
            Opportunity.Create("HDFC Portal Redesign", customers[4].Id, 6500000m, OpportunityStage.Negotiation, 80, today.AddDays(15), vikram.Id, notes: "Security assessment cleared"),
            Opportunity.Create("Airtel Tower Telemetry", customers[5].Id, 5000000m, OpportunityStage.Qualification, 30, today.AddDays(90), vikram.Id, notes: "IoT monitoring expansion"),
            Opportunity.Create("Bajaj Wealth Management Portal", customers[7].Id, 7200000m, OpportunityStage.Negotiation, 75, today.AddDays(10), neha.Id, notes: "Customer portal enhancement"),
            Opportunity.Create("Godrej Logistics Overhaul", customers[8].Id, 3800000m, OpportunityStage.Proposal, 45, today.AddDays(40), neha.Id, notes: "Warehouse tracking system"),
            Opportunity.Create("Asian Paints Supply Planning", customers[9].Id, 5500000m, OpportunityStage.Qualification, 25, today.AddDays(50), neha.Id, notes: "AI-driven demand forecast"),
            Opportunity.Create("Sun Pharma Clinical Trials DB", customers[11].Id, 11000000m, OpportunityStage.Proposal, 40, today.AddDays(35), arjun.Id, notes: "Compliance & audit tracking"),
            Opportunity.Create("Adani Maritime Tracking", customers[12].Id, 14000000m, OpportunityStage.Negotiation, 85, today.AddDays(12), arjun.Id, notes: "Harbour management suite"),
            Opportunity.Create("ITC Agriculture Connect", customers[13].Id, 4200000m, OpportunityStage.Qualification, 20, today.AddDays(60), arjun.Id, notes: "Farmer outreach network"),
            Opportunity.Create("Mahindra EV Telematics", customers[14].Id, 3500000m, OpportunityStage.Proposal, 50, today.AddDays(25), arjun.Id, notes: "Electric commercial fleet"),

            // Won opportunities (with ClosedDate set in recent months)
            Opportunity.Create("Wipro Security Suite", customers[2].Id, 1500000m, OpportunityStage.Won, 100, today.AddDays(-30), asha.Id, notes: "Signed annual subscription", closeNote: "Won contract on security score", closedDate: today.AddDays(-28)),
            Opportunity.Create("L&T Smart Factory", customers[6].Id, 9500000m, OpportunityStage.Won, 100, today.AddDays(-60), vikram.Id, notes: "Industry 4.0 IoT deployment", closeNote: "Master service agreement finalized", closedDate: today.AddDays(-55)),
            Opportunity.Create("Titan Omnichannel System", customers[10].Id, 6000000m, OpportunityStage.Won, 100, today.AddDays(-90), neha.Id, notes: "Store inventory integration", closeNote: "Won against global competitor", closedDate: today.AddDays(-85)),
            Opportunity.Create("Mahindra Fleet Management", customers[14].Id, 8500000m, OpportunityStage.Won, 100, today.AddDays(-10), arjun.Id, notes: "Logistics tracking tier 1", closeNote: "Approved by board", closedDate: today.AddDays(-8)),
            Opportunity.Create("Reliance Inventory Sync", customers[3].Id, 7000000m, OpportunityStage.Won, 100, today.AddDays(-120), vikram.Id, notes: "Distribution center integration", closeNote: "Successful proof of concept", closedDate: today.AddDays(-115)),

            // Lost opportunities
            Opportunity.Create("L&T Supply Chain Portal", customers[6].Id, 4000000m, OpportunityStage.Lost, 0, today.AddDays(-30), vikram.Id, notes: "Vendor portal project", closeNote: "Client deferred budget", closedDate: today.AddDays(-25)),
            Opportunity.Create("Titan Billing System", customers[10].Id, 2000000m, OpportunityStage.Lost, 0, today.AddDays(-60), neha.Id, notes: "POS replacement", closeNote: "Selected lower-cost vendor", closedDate: today.AddDays(-50))
        };

        dbContext.Opportunities.AddRange(opportunities);
        await dbContext.SaveChangesAsync(ct);

        // 5. Seed Follow-ups (30)
        var followUps = new List<FollowUp>
        {
            // Overdue planned follow-ups (FollowUpDate in past)
            FollowUp.Create(customers[0].Id, null, null, now.AddDays(-3), "Follow up on cloud RFP submission", FollowUpType.Call, "Reminder sent via email", asha.Id, false),
            FollowUp.Create(null, leads[0].Id, null, now.AddDays(-2), "Initial qualification discussion", FollowUpType.Call, "Called twice; busy", asha.Id, false),
            FollowUp.Create(customers[3].Id, null, null, now.AddDays(-4), "Review hardware deployment schedule", FollowUpType.Meeting, "Waiting on client engineering team", vikram.Id, false),
            FollowUp.Create(null, null, opportunities[3].Id, now.AddDays(-1), "Follow up on store automation commercial terms", FollowUpType.Call, "Pending procurement signoff", vikram.Id, false),
            FollowUp.Create(customers[7].Id, null, null, now.AddDays(-3), "Review Bajaj customer security checklist", FollowUpType.Email, "Legal terms sent", neha.Id, false),
            FollowUp.Create(null, leads[15].Id, null, now.AddDays(-2), "Demo Mamaearth reporting dashboard", FollowUpType.Call, "Scheduled initial demo", neha.Id, false),
            FollowUp.Create(null, null, opportunities[10].Id, now.AddDays(-5), "Check harbour IoT pilot milestones", FollowUpType.Visit, "Port visit planned", arjun.Id, false),

            // Due today planned follow-ups
            FollowUp.Create(customers[1].Id, null, null, now.AddHours(2), "Contract clause review with Infosys Legal", FollowUpType.Meeting, "Discuss SLA commitments", asha.Id, false),
            FollowUp.Create(null, leads[12].Id, null, now.AddHours(4), "PhonePe enterprise requirements call", FollowUpType.Call, "Prepare slide deck", asha.Id, false),
            FollowUp.Create(customers[4].Id, null, null, now.AddHours(1), "HDFC Portal milestone kickoff", FollowUpType.Meeting, "Final architecture review", vikram.Id, false),
            FollowUp.Create(null, null, opportunities[6].Id, now.AddHours(3), "Bajaj commercial pricing call", FollowUpType.Call, "Confirm payment terms", neha.Id, false),
            FollowUp.Create(customers[11].Id, null, null, now.AddHours(5), "Sun Pharma compliance walk-through", FollowUpType.Meeting, "With Head of Quality", arjun.Id, false),

            // Upcoming planned follow-ups (next 7 days to next month)
            FollowUp.Create(customers[2].Id, null, null, now.AddDays(2), "Quarterly analytics roadmap review", FollowUpType.Meeting, "Agenda confirmed", asha.Id, false),
            FollowUp.Create(null, leads[1].Id, null, now.AddDays(3), "Swiggy B2B partnership proposal", FollowUpType.Email, "Drafting commercial proposal", asha.Id, false),
            FollowUp.Create(null, null, opportunities[0].Id, now.AddDays(4), "TCS technical due diligence meeting", FollowUpType.Meeting, "Presenting architecture", asha.Id, false),
            FollowUp.Create(customers[5].Id, null, null, now.AddDays(5), "Airtel network integration deep dive", FollowUpType.Meeting, "Network team attending", vikram.Id, false),
            FollowUp.Create(null, leads[2].Id, null, now.AddDays(2), "Razorpay technical architecture review", FollowUpType.Call, "With tech leads", vikram.Id, false),
            FollowUp.Create(null, null, opportunities[4].Id, now.AddDays(6), "HDFC executive sponsorship briefing", FollowUpType.Meeting, "C-level meeting", vikram.Id, false),
            FollowUp.Create(customers[8].Id, null, null, now.AddDays(3), "Godrej warehouse site survey", FollowUpType.Visit, "Vikhroli plant", neha.Id, false),
            FollowUp.Create(null, leads[4].Id, null, now.AddDays(4), "PolicyBazaar discovery session", FollowUpType.Call, "Product demo walkthrough", neha.Id, false),
            FollowUp.Create(null, null, opportunities[7].Id, now.AddDays(7), "Godrej procurement meeting", FollowUpType.Meeting, "Negotiate discounting", neha.Id, false),
            FollowUp.Create(customers[12].Id, null, null, now.AddDays(2), "Adani port logistics coordination", FollowUpType.Call, "Operations sync", arjun.Id, false),
            FollowUp.Create(null, leads[5].Id, null, now.AddDays(5), "Nykaa brand integration discussion", FollowUpType.Email, "Send capability deck", arjun.Id, false),
            FollowUp.Create(null, null, opportunities[12].Id, now.AddDays(6), "Mahindra EV firmware telemetry meeting", FollowUpType.Meeting, "With R&D department", arjun.Id, false),

            // Completed follow-ups
            FollowUp.Create(customers[0].Id, null, null, now.AddDays(-15), "Introductory executive meeting with TCS", FollowUpType.Meeting, "Great alignment on enterprise strategy.", asha.Id, true),
            FollowUp.Create(customers[1].Id, null, null, now.AddDays(-10), "Infosys POC presentation", FollowUpType.Meeting, "POC successfully accepted.", asha.Id, true),
            FollowUp.Create(customers[3].Id, null, null, now.AddDays(-20), "Reliance executive sponsor lunch", FollowUpType.Meeting, "Built executive relationship.", vikram.Id, true),
            FollowUp.Create(customers[6].Id, null, null, now.AddDays(-40), "L&T Smart factory vendor review", FollowUpType.Meeting, "Cleared preliminary vendor assessment.", vikram.Id, true),
            FollowUp.Create(customers[7].Id, null, null, now.AddDays(-12), "Bajaj wealth portal requirements session", FollowUpType.Call, "Detailed specifications recorded.", neha.Id, true),
            FollowUp.Create(customers[14].Id, null, null, now.AddDays(-25), "Mahindra Fleet kickoff meeting", FollowUpType.Meeting, "Project Charter signed.", arjun.Id, true)
        };

        dbContext.FollowUps.AddRange(followUps);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Demo data seeding completed successfully.");
        PrintCredentialsTable();
    }

    private async Task<ApplicationUser> EnsureUserAsync(
        string email,
        string fullName,
        string role,
        string password,
        string? managerId,
        DateTime now)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing != null)
        {
            return existing;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            ManagerId = managerId,
            IsActive = true,
            EmailConfirmed = true,
            CreatedDate = now
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create demo user {email}: {errors}");
        }

        var roleResult = await userManager.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
        {
            var errors = string.Join("; ", roleResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to add user {email} to role {role}: {errors}");
        }

        return user;
    }

    private static void PrintCredentialsTable()
    {
        Console.WriteLine();
        Console.WriteLine("==========================================================================================");
        Console.WriteLine("                              ACXIOM CRM DEMO ACCOUNTS                                    ");
        Console.WriteLine("==========================================================================================");
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0,-16} | {1,-34} | {2,-20}", "Role", "Email", "Password"));
        Console.WriteLine("-----------------+------------------------------------+-----------------------------------");
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0,-16} | {1,-34} | {2,-20}", "Admin", "admin@acxiomcrm.local", AdminPassword));
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0,-16} | {1,-34} | {2,-20}", "Manager", "rohan.mehta@acxiomcrm.local", ManagerPassword));
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0,-16} | {1,-34} | {2,-20}", "Manager", "priya.nair@acxiomcrm.local", ManagerPassword));
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0,-16} | {1,-34} | {2,-20}", "SalesExecutive", "asha.kulkarni@acxiomcrm.local", ExecutivePassword));
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0,-16} | {1,-34} | {2,-20}", "SalesExecutive", "vikram.singh@acxiomcrm.local", ExecutivePassword));
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0,-16} | {1,-34} | {2,-20}", "SalesExecutive", "neha.iyer@acxiomcrm.local", ExecutivePassword));
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0,-16} | {1,-34} | {2,-20}", "SalesExecutive", "arjun.reddy@acxiomcrm.local", ExecutivePassword));
        Console.WriteLine("==========================================================================================");
        Console.WriteLine();
    }
}
