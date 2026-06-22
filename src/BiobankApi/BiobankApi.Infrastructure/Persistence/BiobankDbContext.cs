using System.Text.Json;
using BiobankApi.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BiobankApi.Infrastructure.Persistence;

/// <summary>EF Core context for the biobank_api database (normalized patient schema).</summary>
public sealed class BiobankDbContext(DbContextOptions<BiobankDbContext> options) : DbContext(options)
{
    public DbSet<PatientEntity> Patients => Set<PatientEntity>();
    public DbSet<TissueSampleEntity> TissueSamples => Set<TissueSampleEntity>();
    public DbSet<SerumSampleEntity> SerumSamples => Set<SerumSampleEntity>();
    public DbSet<GenomeSampleEntity> GenomeSamples => Set<GenomeSampleEntity>();
    public DbSet<DiagnosticSpecimenEntity> DiagnosticSpecimens => Set<DiagnosticSpecimenEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // List<string> stored as a JSON text column (portable across PostgreSQL and SQLite).
        var jsonConverter = new ValueConverter<List<string>, string>(
            value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
            text => JsonSerializer.Deserialize<List<string>>(text, (JsonSerializerOptions?)null) ?? new List<string>());

        var jsonComparer = new ValueComparer<List<string>>(
            (left, right) => (left ?? new List<string>()).SequenceEqual(right ?? new List<string>()),
            value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode(StringComparison.Ordinal))),
            value => value.ToList());

        modelBuilder.Entity<PatientEntity>(builder =>
        {
            builder.ToTable("patient");
            builder.HasKey(patient => patient.PatientId);
            builder.Property(patient => patient.Sex).HasConversion<string>();
            builder.Property(patient => patient.AccessionNumbers)
                .HasConversion(jsonConverter, jsonComparer);

            builder.HasMany(patient => patient.TissueSamples)
                .WithOne()
                .HasForeignKey(sample => sample.PatientId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(patient => patient.SerumSamples)
                .WithOne()
                .HasForeignKey(sample => sample.PatientId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(patient => patient.GenomeSamples)
                .WithOne()
                .HasForeignKey(sample => sample.PatientId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(patient => patient.DiagnosticSpecimens)
                .WithOne()
                .HasForeignKey(specimen => specimen.PatientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        ConfigureSampleTable<TissueSampleEntity>(modelBuilder, "tissue_sample", jsonConverter, jsonComparer);
        ConfigureSampleTable<SerumSampleEntity>(modelBuilder, "serum_sample", jsonConverter, jsonComparer);
        ConfigureSampleTable<GenomeSampleEntity>(modelBuilder, "genome_sample", jsonConverter, jsonComparer);

        modelBuilder.Entity<TissueSampleEntity>().Property(sample => sample.Retrieved).HasConversion<string>();
        modelBuilder.Entity<SerumSampleEntity>().Property(sample => sample.Retrieved).HasConversion<string>();
        modelBuilder.Entity<GenomeSampleEntity>().Property(sample => sample.Retrieved).HasConversion<string>();

        modelBuilder.Entity<DiagnosticSpecimenEntity>(builder =>
        {
            builder.ToTable("diagnostic_specimen");
            builder.HasKey(specimen => specimen.SampleId);
            builder.HasIndex(specimen => specimen.PatientId);
            builder.Property(specimen => specimen.Retrieved).HasConversion<string>();
        });
    }

    private static void ConfigureSampleTable<TEntity>(
        ModelBuilder modelBuilder,
        string tableName,
        ValueConverter<List<string>, string> jsonConverter,
        ValueComparer<List<string>> jsonComparer)
        where TEntity : SampleEntityBase
    {
        modelBuilder.Entity<TEntity>(builder =>
        {
            builder.ToTable(tableName);
            builder.HasKey(sample => sample.SampleId);
            builder.HasIndex(sample => sample.PatientId);
            builder.Property(sample => sample.AccessionNumbers)
                .HasConversion(jsonConverter, jsonComparer);
        });
    }
}
