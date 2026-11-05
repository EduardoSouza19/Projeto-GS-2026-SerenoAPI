using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SerenoAPI.Models;

namespace SerenoAPI.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Estacao> Estacoes { get; set; }

    public DbSet<Equipamento> Equipamentos { get; set; }

    public DbSet<Reporte> Reportes { get; set; }

    public DbSet<IdempotencyRecord> IdempotencyRecords { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Estacao>(e =>
        {
            e.ToTable("ESTACOES");
            e.Property(x => x.Nome).HasMaxLength(100).IsRequired();
            e.Property(x => x.Linha).HasMaxLength(60).IsRequired();
            e.Property(x => x.Endereco).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<Equipamento>(e =>
        {
            e.ToTable("EQUIPAMENTOS");
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.StatusAtual).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Localizacao).HasMaxLength(150).IsRequired();
            e.HasOne(x => x.Estacao).WithMany(x => x.Equipamentos)
                .HasForeignKey(x => x.EstacaoId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Reporte>(e =>
        {
            e.ToTable("REPORTES");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Observacao).HasMaxLength(300);
            e.HasOne(x => x.Equipamento).WithMany(x => x.Reportes)
                .HasForeignKey(x => x.EquipamentoId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IdempotencyRecord>(e =>
        {
            e.ToTable("IDEMPOTENCY_RECORDS");
            e.Property(x => x.Chave).HasMaxLength(100).IsRequired();
            e.Property(x => x.HashDoCorpo).HasMaxLength(64).IsRequired();
            e.Property(x => x.CorpoDaResposta).HasMaxLength(2000).IsRequired();
            e.HasIndex(x => new { x.Chave, x.UsuarioId }).IsUnique();
        });

        // Colunas em MAIÚSCULO com "_" (ex.: STATUS_ATUAL) para o SELECT no Oracle ficar simples
        modelBuilder.Model.GetEntityTypes()
            .SelectMany(entidade => entidade.GetProperties())
            .ToList()
            .ForEach(propriedade => propriedade.SetColumnName(
                Regex.Replace(propriedade.Name, "(?<!^)([A-Z])", "_$1").ToUpper()));
    }
}
