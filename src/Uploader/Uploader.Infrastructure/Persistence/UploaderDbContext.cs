using Microsoft.EntityFrameworkCore;
using Uploader.Infrastructure.Persistence.Entities;

namespace Uploader.Infrastructure.Persistence;

/// <summary>EF Core context for the uploader's sync-state database.</summary>
public sealed class UploaderDbContext : DbContext
{
    public UploaderDbContext(DbContextOptions<UploaderDbContext> options) : base(options)
    {
    }

    public DbSet<SyncRunEntity> SyncRuns => Set<SyncRunEntity>();
    public DbSet<PatientSyncStateEntity> PatientSyncStates => Set<PatientSyncStateEntity>();
    public DbSet<SampleSyncStateEntity> SampleSyncStates => Set<SampleSyncStateEntity>();
    public DbSet<SequencingSyncStateEntity> SequencingSyncStates => Set<SequencingSyncStateEntity>();
    public DbSet<WsiSyncStateEntity> WsiSyncStates => Set<WsiSyncStateEntity>();
    public DbSet<ImagingStudySyncStateEntity> ImagingStudySyncStates => Set<ImagingStudySyncStateEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SyncRunEntity>(builder =>
        {
            builder.ToTable("sync_run");
            builder.HasKey(run => run.Id);
        });

        modelBuilder.Entity<PatientSyncStateEntity>(builder =>
        {
            builder.ToTable("patient_sync_state");
            builder.HasKey(state => state.Id);
            ConfigureCommon(builder);

            builder.HasMany(state => state.Samples)
                .WithOne()
                .HasForeignKey(sample => sample.PatientId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(state => state.ImagingStudies)
                .WithOne()
                .HasForeignKey(study => study.PatientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SampleSyncStateEntity>(builder =>
        {
            builder.ToTable("sample_sync_state");
            builder.HasKey(state => state.Id);
            ConfigureCommon(builder);

            builder.HasOne(state => state.Sequencing)
                .WithOne()
                .HasForeignKey<SequencingSyncStateEntity>(sequencing => sequencing.SampleId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(state => state.Wsi)
                .WithOne()
                .HasForeignKey<WsiSyncStateEntity>(wsi => wsi.SampleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SequencingSyncStateEntity>(builder =>
        {
            builder.ToTable("sequencing_sync_state");
            builder.HasKey(state => state.Id);
            ConfigureCommon(builder);
        });

        modelBuilder.Entity<WsiSyncStateEntity>(builder =>
        {
            builder.ToTable("wsi_sync_state");
            builder.HasKey(state => state.Id);
            ConfigureCommon(builder);
        });

        modelBuilder.Entity<ImagingStudySyncStateEntity>(builder =>
        {
            builder.ToTable("imaging_study_sync_state");
            builder.HasKey(state => state.Id);
            ConfigureCommon(builder);
        });
    }

    private static void ConfigureCommon<TEntity>(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> builder)
        where TEntity : SyncStateEntityBase
    {
        builder.Property(state => state.Status).HasConversion<string>();
        builder.HasIndex(state => state.Status);
        builder.HasIndex(state => state.RunId);
    }
}
