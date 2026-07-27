using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SequencingApi.Infrastructure.Persistence.Entities;

namespace SequencingApi.Infrastructure.Persistence;

/// <summary>EF Core context for the sequencing_api database.</summary>
/// <remarks>
/// One table per domain concept, snake_case table names with EF's default column names — the same
/// shape as <c>BiobankDbContext</c>. Enums are stored as text so a value stays readable in the
/// database and re-ordering the enum cannot silently re-interpret existing rows.
/// <para>
/// The domain's 0..1 value objects (library preparation, quality metrics) get their own tables keyed
/// on the owner's primary key, rather than being spread across the owner as a nullable column group:
/// a row that exists or does not is precisely what "0..1" means, and it needs no marker column to
/// tell an absent value object from one whose every field is unknown.
/// </para>
/// </remarks>
public sealed class SequencingDbContext : DbContext
{
    public SequencingDbContext(DbContextOptions<SequencingDbContext> options) : base(options)
    {
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Source dates are zone-less calendar values; timestamp-without-time-zone accepts
        // DateTimeKind.Unspecified, unlike timestamptz which only accepts UTC.
        configurationBuilder.Properties<DateTime>().HaveColumnType("timestamp without time zone");
    }

    public DbSet<SampleEntity> Samples => Set<SampleEntity>();
    public DbSet<RunSampleEntity> RunSamples => Set<RunSampleEntity>();
    public DbSet<LibraryPreparationEntity> LibraryPreparations => Set<LibraryPreparationEntity>();
    public DbSet<AnalysisEntity> Analyses => Set<AnalysisEntity>();
    public DbSet<QualityMetricsEntity> QualityMetrics => Set<QualityMetricsEntity>();
    public DbSet<SequencingFileEntity> SequencingFiles => Set<SequencingFileEntity>();
    public DbSet<SequencingRunEntity> SequencingRuns => Set<SequencingRunEntity>();
    public DbSet<RunReadEntity> RunReads => Set<RunReadEntity>();
    public DbSet<PanelEntity> Panels => Set<PanelEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // List<string> stored as a JSON text column (portable across PostgreSQL and SQLite), as
        // patient.AccessionNumbers is. A scalar list, unlike the structured reads, which are rows.
        var jsonConverter = new ValueConverter<List<string>, string>(
            value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
            text => JsonSerializer.Deserialize<List<string>>(text, (JsonSerializerOptions?)null) ?? new List<string>());

        var jsonComparer = new ValueComparer<List<string>>(
            (left, right) => (left ?? new List<string>()).SequenceEqual(right ?? new List<string>()),
            value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode(StringComparison.Ordinal))),
            value => value.ToList());

        modelBuilder.Entity<SampleEntity>(builder =>
        {
            builder.ToTable("sample");
            builder.HasKey(sample => sample.ExternalId);
            builder.HasIndex(sample => sample.IdScheme);

            // The uploader's only entry point is by predictive number, so this lookup is the hot path.
            // Not unique: two samples can carry the same one, and most carry none at all.
            builder.HasIndex(sample => sample.PredictiveNumber);

            builder.HasMany(sample => sample.RunSamples)
                .WithOne()
                .HasForeignKey(runSample => runSample.SampleExternalId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RunSampleEntity>(builder =>
        {
            builder.ToTable("run_sample");
            builder.HasKey(runSample => runSample.Id);
            builder.Property(runSample => runSample.Id).ValueGeneratedOnAdd();
            builder.Property(runSample => runSample.SampleType).HasConversion<string>();

            // The same invariant SampleAggregate.Create guards: a sample is sequenced at most once
            // per run, so a run id appears at most once inside one sample.
            builder.HasIndex(runSample => new { runSample.SampleExternalId, runSample.RunId }).IsUnique();

            // Indexed, not constrained: runs are a separate aggregate root saved independently, so
            // requiring their rows to exist first would make ingest order load-bearing.
            builder.HasIndex(runSample => runSample.RunId);

            builder.HasOne(runSample => runSample.LibraryPreparation)
                .WithOne()
                .HasForeignKey<LibraryPreparationEntity>(library => library.RunSampleId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(runSample => runSample.Files)
                .WithOne()
                .HasForeignKey(file => file.RunSampleId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(runSample => runSample.Analyses)
                .WithOne()
                .HasForeignKey(analysis => analysis.RunSampleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LibraryPreparationEntity>(builder =>
        {
            builder.ToTable("library_preparation");
            // Key on the owner's id: that is what makes this one-to-one rather than one-to-many.
            builder.HasKey(library => library.RunSampleId);
            builder.Property(library => library.RunSampleId).ValueGeneratedNever();
            // Indexed for panel-usage queries; not a foreign key, panels are their own root.
            builder.HasIndex(library => library.PanelId);
        });

        modelBuilder.Entity<AnalysisEntity>(builder =>
        {
            builder.ToTable("analysis");
            builder.HasKey(analysis => analysis.Id);
            builder.Property(analysis => analysis.Id).ValueGeneratedOnAdd();
            builder.Property(analysis => analysis.AnalysisType).HasConversion<string>();
            builder.HasIndex(analysis => analysis.RunSampleId);

            builder.HasOne(analysis => analysis.Quality)
                .WithOne()
                .HasForeignKey<QualityMetricsEntity>(quality => quality.AnalysisId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(analysis => analysis.Files)
                .WithOne()
                .HasForeignKey(file => file.AnalysisId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QualityMetricsEntity>(builder =>
        {
            builder.ToTable("quality_metrics");
            builder.HasKey(quality => quality.AnalysisId);
            builder.Property(quality => quality.AnalysisId).ValueGeneratedNever();
            builder.Property(quality => quality.Verdict).HasConversion<string>();
        });

        modelBuilder.Entity<SequencingFileEntity>(builder =>
        {
            // A file belongs to exactly one owner - the sequencing itself or one analysis of it.
            // Enforced in the database rather than trusted to the mapper; the expression is valid
            // on both PostgreSQL and the SQLite used by the integration tests.
            builder.ToTable("sequencing_file", table => table.HasCheckConstraint(
                "CK_sequencing_file_single_owner",
                "(\"RunSampleId\" IS NULL) <> (\"AnalysisId\" IS NULL)"));
            builder.HasKey(file => file.Id);
            builder.Property(file => file.Id).ValueGeneratedOnAdd();
            builder.Property(file => file.Role).HasConversion<string>();
            builder.HasIndex(file => file.Role);
        });

        modelBuilder.Entity<SequencingRunEntity>(builder =>
        {
            builder.ToTable("sequencing_run");
            builder.HasKey(run => run.RunId);
            builder.HasIndex(run => run.RunDate);

            builder.HasMany(run => run.Reads)
                .WithOne()
                .HasForeignKey(read => read.RunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RunReadEntity>(builder =>
        {
            builder.ToTable("run_read");
            builder.HasKey(read => read.Id);
            builder.Property(read => read.Id).ValueGeneratedOnAdd();
            // The read structure is an ordered sequence, so a run cannot hold two reads at one
            // position - and the order has to be restored explicitly on the way out.
            builder.HasIndex(read => new { read.RunId, read.Position }).IsUnique();
        });

        modelBuilder.Entity<PanelEntity>(builder =>
        {
            builder.ToTable("panel");
            builder.HasKey(panel => panel.PanelId);
            builder.Property(panel => panel.Genes).HasConversion(jsonConverter, jsonComparer);
        });
    }
}
