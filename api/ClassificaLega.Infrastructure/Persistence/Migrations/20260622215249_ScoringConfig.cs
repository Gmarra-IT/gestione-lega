using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassificaLega.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ScoringConfig : Migration
    {
        // Regola di default (riversa il cablato storico), serializzata camelCase come da converter.
        private const string DefaultScoringRuleJson =
            "{\"pointsPerWin\":3,\"pointsPerDraw\":1,\"pointsPerLoss\":0," +
            "\"positionBonuses\":[]," +
            "\"scoreBonuses\":[" +
            "{\"fromMatchPoints\":6,\"points\":1},{\"fromMatchPoints\":7,\"points\":2}," +
            "{\"fromMatchPoints\":8,\"points\":3},{\"fromMatchPoints\":9,\"points\":4}," +
            "{\"fromMatchPoints\":10,\"points\":6},{\"fromMatchPoints\":12,\"points\":8}]," +
            "\"participationTiers\":[" +
            "{\"fromTournament\":1,\"pointsPerParticipation\":1}," +
            "{\"fromTournament\":6,\"pointsPerParticipation\":2}]}";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rinomini con preservazione dati: BonusRisultato→ScoreBonus, BonusPartecipazione→ParticipationPoints.
            migrationBuilder.RenameColumn(
                name: "BonusRisultato",
                table: "Results",
                newName: "ScoreBonus");

            migrationBuilder.RenameColumn(
                name: "BonusPartecipazione",
                table: "Results",
                newName: "ParticipationPoints");

            // ScoringRule jsonb: default = regola storica così le stagioni esistenti restano valide.
            migrationBuilder.AddColumn<string>(
                name: "ScoringRule",
                table: "Seasons",
                type: "jsonb",
                nullable: false,
                defaultValue: DefaultScoringRuleJson);

            // Nuova colonna bonus piazzamento (0 per i dati esistenti, nessuna Position seedata).
            migrationBuilder.AddColumn<int>(
                name: "PositionBonus",
                table: "Results",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Wins",
                table: "Results",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Draws",
                table: "Results",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Losses",
                table: "Results",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Position",
                table: "Results",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScoringRule",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "PositionBonus",
                table: "Results");

            migrationBuilder.DropColumn(
                name: "Wins",
                table: "Results");

            migrationBuilder.DropColumn(
                name: "Draws",
                table: "Results");

            migrationBuilder.DropColumn(
                name: "Losses",
                table: "Results");

            migrationBuilder.DropColumn(
                name: "Position",
                table: "Results");

            migrationBuilder.RenameColumn(
                name: "ScoreBonus",
                table: "Results",
                newName: "BonusRisultato");

            migrationBuilder.RenameColumn(
                name: "ParticipationPoints",
                table: "Results",
                newName: "BonusPartecipazione");
        }
    }
}
