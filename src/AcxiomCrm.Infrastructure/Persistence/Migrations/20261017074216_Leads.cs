using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AcxiomCrm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Leads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "seq_LeadCode",
                startValue: 1L);

            migrationBuilder.CreateTable(
                name: "Leads",
                columns: table => new
                {
                    LeadId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeadCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValueSql: "'LEAD-' + FORMAT(NEXT VALUE FOR seq_LeadCode, '000000')"),
                    LeadName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    EmailNormalized = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Source = table.Column<byte>(type: "tinyint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Priority = table.Column<byte>(type: "tinyint", nullable: false),
                    ExpectedValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ConvertedCustomerId = table.Column<int>(type: "int", nullable: true),
                    ConvertedOpportunityId = table.Column<int>(type: "int", nullable: true),
                    ConvertedDate = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedDate = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leads", x => x.LeadId);
                    table.CheckConstraint("CK_Leads_Contact", "Email IS NOT NULL OR Phone IS NOT NULL");
                    table.CheckConstraint("CK_Leads_Converted", "(Status = 5 AND ConvertedCustomerId IS NOT NULL) OR (Status <> 5 AND ConvertedCustomerId IS NULL)");
                    table.CheckConstraint("CK_Leads_ExpectedValue", "ExpectedValue >= 0 AND ExpectedValue <= 100000000");
                    table.CheckConstraint("CK_Leads_Priority", "Priority IN (1, 2, 3)");
                    table.CheckConstraint("CK_Leads_Source", "Source IN (1, 2, 3, 4, 5, 6, 7)");
                    table.CheckConstraint("CK_Leads_Status", "Status IN (1, 2, 3, 4, 5, 6)");
                    table.ForeignKey(
                        name: "FK_Leads_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Leads_Customers_ConvertedCustomerId",
                        column: x => x.ConvertedCustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_ConvertedCustomerId",
                table: "Leads",
                column: "ConvertedCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_LeadName",
                table: "Leads",
                column: "LeadName");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_OwnerId_Status",
                table: "Leads",
                columns: new[] { "OwnerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_Status_CreatedDate",
                table: "Leads",
                columns: new[] { "Status", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "UX_Leads_LeadCode",
                table: "Leads",
                column: "LeadCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Leads");

            migrationBuilder.DropSequence(
                name: "seq_LeadCode");
        }
    }
}
