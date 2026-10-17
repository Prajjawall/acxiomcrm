using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AcxiomCrm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Opportunities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Opportunities",
                columns: table => new
                {
                    OpportunityId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OpportunityName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    LeadId = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Stage = table.Column<byte>(type: "tinyint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Probability = table.Column<byte>(type: "tinyint", nullable: false),
                    ExpectedCloseDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ClosedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CloseNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Source = table.Column<byte>(type: "tinyint", nullable: true),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_Opportunities", x => x.OpportunityId);
                    table.CheckConstraint("CK_Opps_Amount", "Amount >= 0 AND Amount <= 1000000000");
                    table.CheckConstraint("CK_Opps_ClosedDate", "(Status = 1 AND ClosedDate IS NULL) OR (Status <> 1 AND ClosedDate IS NOT NULL)");
                    table.CheckConstraint("CK_Opps_OpenAmountPositive", "Status <> 1 OR Amount > 0");
                    table.CheckConstraint("CK_Opps_Probability", "Probability >= 0 AND Probability <= 100");
                    table.CheckConstraint("CK_Opps_Stage", "Stage IN (1, 2, 3, 4, 5)");
                    table.CheckConstraint("CK_Opps_StageStatus", "(Stage IN (1, 2, 3) AND Status = 1) OR (Stage = 4 AND Status = 2) OR (Stage = 5 AND Status = 3)");
                    table.CheckConstraint("CK_Opps_Status", "Status IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_Opportunities_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Opportunities_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Opportunities_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "LeadId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Opportunities_LeadId",
                table: "Opportunities",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_Opps_CustomerId",
                table: "Opportunities",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Opps_OwnerId_Status",
                table: "Opportunities",
                columns: new[] { "OwnerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Opps_Stage_ExpectedCloseDate",
                table: "Opportunities",
                columns: new[] { "Stage", "ExpectedCloseDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Opps_Status_ClosedDate",
                table: "Opportunities",
                columns: new[] { "Status", "ClosedDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Opportunities");
        }
    }
}
