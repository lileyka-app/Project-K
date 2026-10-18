using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectK.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKurinDuesArticles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Articles",
                table: "KurinDuesRates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<decimal>(
                name: "KurinReduced",
                table: "KurinDuesRates",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            // Every rate set so far becomes the two статті it already was: the станиця part with its
            // пільгова amount, and the kurin part, which the пільга did not touch.
            migrationBuilder.Sql("""
                UPDATE KurinDuesRates
                SET KurinReduced = KurinShare,
                    Articles = N'[{"Name":"Станиця","Recipient":0,"Amount":' + CONVERT(nvarchar(40), StanytsiaFull)
                        + N',"ReducedAmount":' + CONVERT(nvarchar(40), StanytsiaReduced)
                        + N'},{"Name":"Курінь","Recipient":1,"Amount":' + CONVERT(nvarchar(40), KurinShare)
                        + N',"ReducedAmount":' + CONVERT(nvarchar(40), KurinShare) + N'}]'
                WHERE Articles = N'[]';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Articles",
                table: "KurinDuesRates");

            migrationBuilder.DropColumn(
                name: "KurinReduced",
                table: "KurinDuesRates");
        }
    }
}
