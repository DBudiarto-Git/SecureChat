using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureChat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRsaPublicKeyToProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RsaPublicKeyXml",
                table: "UserProfiles",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RsaPublicKeyXml",
                table: "UserProfiles");
        }
    }
}
