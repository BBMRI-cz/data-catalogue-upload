using Microsoft.EntityFrameworkCore;

namespace Uploader.Infrastructure.Persistence;

/// <summary>EF Core context for the uploader's sync-state database.</summary>
public sealed class UploaderDbContext(DbContextOptions<UploaderDbContext> options) : DbContext(options)
{
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
            builder.HasKey(state => state.PatientId);
            ConfigureCommon(builder);
        });

        modelBuilder.Entity<SampleSyncStateEntity>(builder =>
        {
            builder.ToTable("sample_sync_state");
            builder.HasKey(state => state.SampleId);
            builder.HasIndex(state => state.PatientId);
            ConfigureCommon(builder);
            builder.HasOne<PatientSyncStateEntity>()
                .WithMany()
                .HasForeignKey(state => state.PatientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SequencingSyncStateEntity>(builder =>
        {
            builder.ToTable("sequencing_sync_state");
            builder.HasKey(state => state.PredictiveNumber);
            builder.HasIndex(state => state.SampleId);
            ConfigureCommon(builder);
            builder.HasOne<SampleSyncStateEntity>()
                .WithMany()
                .HasForeignKey(state => state.SampleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WsiSyncStateEntity>(builder =>
        {
            builder.ToTable("wsi_sync_state");
            builder.HasKey(state => state.BiopticNumber);
            builder.HasIndex(state => state.SampleId);
            ConfigureCommon(builder);
            builder.HasOne<SampleSyncStateEntity>()
                .WithMany()
                .HasForeignKey(state => state.SampleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ImagingStudySyncStateEntity>(builder =>
        {
            builder.ToTable("imaging_study_sync_state");
            builder.HasKey(state => state.AccessionNumber);
            builder.HasIndex(state => state.PatientId);
            ConfigureCommon(builder);
            builder.HasOne<PatientSyncStateEntity>()
                .WithMany()
                .HasForeignKey(state => state.PatientId)
                .OnDelete(DeleteBehavior.Cascade);
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
