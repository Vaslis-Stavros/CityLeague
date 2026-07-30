using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CityLeague.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class LeagueTeamsProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LeagueParticipants_LeagueId",
                table: "LeagueParticipants");

            migrationBuilder.DropIndex(
                name: "IX_LeagueEvents_LeagueId",
                table: "LeagueEvents");

            migrationBuilder.AddColumn<Guid>(
                name: "LeaderUserId",
                table: "LeagueTeams",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "LeagueTeams",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PlannedMatchCount",
                table: "Leagues",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartedAt",
                table: "Leagues",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeagueTeams_LeaderUserId",
                table: "LeagueTeams",
                column: "LeaderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeagueParticipants_LeagueId_UserId",
                table: "LeagueParticipants",
                columns: new[] { "LeagueId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeagueEvents_LeagueId_EventId",
                table: "LeagueEvents",
                columns: new[] { "LeagueId", "EventId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LeagueTeams_Users_LeaderUserId",
                table: "LeagueTeams",
                column: "LeaderUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LeagueTeams_Users_LeaderUserId",
                table: "LeagueTeams");

            migrationBuilder.DropIndex(
                name: "IX_LeagueTeams_LeaderUserId",
                table: "LeagueTeams");

            migrationBuilder.DropIndex(
                name: "IX_LeagueParticipants_LeagueId_UserId",
                table: "LeagueParticipants");

            migrationBuilder.DropIndex(
                name: "IX_LeagueEvents_LeagueId_EventId",
                table: "LeagueEvents");

            migrationBuilder.DropColumn(
                name: "LeaderUserId",
                table: "LeagueTeams");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "LeagueTeams");

            migrationBuilder.DropColumn(
                name: "PlannedMatchCount",
                table: "Leagues");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "Leagues");

            migrationBuilder.CreateIndex(
                name: "IX_LeagueParticipants_LeagueId",
                table: "LeagueParticipants",
                column: "LeagueId");

            migrationBuilder.CreateIndex(
                name: "IX_LeagueEvents_LeagueId",
                table: "LeagueEvents",
                column: "LeagueId");
        }
    }
}
