﻿using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pvinstallations_RothnerLuca.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PvInstallations",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Longitude = table.Column<float>(type: "real", nullable: false),
                    Latitude = table.Column<float>(type: "real", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    OwnerName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PvInstallations", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ProductionReports",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProducedWattage = table.Column<float>(type: "real", nullable: false),
                    HouseholdWattage = table.Column<float>(type: "real", nullable: false),
                    BatteryWattage = table.Column<float>(type: "real", nullable: false),
                    GridWattage = table.Column<float>(type: "real", nullable: false),
                    PvInstallationID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionReports", x => x.ID);
                    table.ForeignKey(
                        name: "FK_ProductionReports_PvInstallations_PvInstallationID",
                        column: x => x.PvInstallationID,
                        principalTable: "PvInstallations",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionReports_PvInstallationID",
                table: "ProductionReports",
                column: "PvInstallationID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductionReports");

            migrationBuilder.DropTable(
                name: "PvInstallations");
        }
    }
}
