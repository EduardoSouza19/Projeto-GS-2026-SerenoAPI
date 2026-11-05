using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SerenoAPI.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ESTACOES",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    NOME = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    LINHA = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: false),
                    ENDERECO = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    LATITUDE = table.Column<double>(type: "BINARY_DOUBLE", nullable: true),
                    LONGITUDE = table.Column<double>(type: "BINARY_DOUBLE", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ESTACOES", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "IDEMPOTENCY_RECORDS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    CHAVE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    USUARIO_ID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    HASH_DO_CORPO = table.Column<string>(type: "NVARCHAR2(64)", maxLength: 64, nullable: false),
                    STATUS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CORPO_DA_RESPOSTA = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    CRIADO_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IDEMPOTENCY_RECORDS", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "EQUIPAMENTOS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ESTACAO_ID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    TIPO = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    LOCALIZACAO = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    STATUS_ATUAL = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    ATUALIZADO_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EQUIPAMENTOS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_EQUIPAMENTOS_ESTACOES_ESTACAO_ID",
                        column: x => x.ESTACAO_ID,
                        principalTable: "ESTACOES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "REPORTES",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    EQUIPAMENTO_ID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    USUARIO_ID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    STATUS = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    OBSERVACAO = table.Column<string>(type: "NVARCHAR2(300)", maxLength: 300, nullable: true),
                    CRIADO_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REPORTES", x => x.ID);
                    table.ForeignKey(
                        name: "FK_REPORTES_EQUIPAMENTOS_EQUIPAMENTO_ID",
                        column: x => x.EQUIPAMENTO_ID,
                        principalTable: "EQUIPAMENTOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EQUIPAMENTOS_ESTACAO_ID",
                table: "EQUIPAMENTOS",
                column: "ESTACAO_ID");

            migrationBuilder.CreateIndex(
                name: "IX_IDEMPOTENCY_RECORDS_CHAVE_USUARIO_ID",
                table: "IDEMPOTENCY_RECORDS",
                columns: new[] { "CHAVE", "USUARIO_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_REPORTES_EQUIPAMENTO_ID",
                table: "REPORTES",
                column: "EQUIPAMENTO_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IDEMPOTENCY_RECORDS");

            migrationBuilder.DropTable(
                name: "REPORTES");

            migrationBuilder.DropTable(
                name: "EQUIPAMENTOS");

            migrationBuilder.DropTable(
                name: "ESTACOES");
        }
    }
}
