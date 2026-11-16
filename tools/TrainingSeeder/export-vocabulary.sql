-- Vocabulary export for the training dataset (#112).
--
-- Reads one source API's database and prints, as JSON, the value frequencies the training seeder
-- draws from. Run it against biobank-db and sequencing-db; it tells the two apart by their tables.
-- From the laptop, so the script goes in on stdin, the JSON comes back on stdout and nothing is
-- written on the server:
--
--   ssh <biobank host> 'docker exec -i biobank-db psql -X -q -U postgres -d biobank_api' \
--     < tools/TrainingSeeder/export-vocabulary.sql > tools/TrainingSeeder/biobank-vocabulary.json
--   ssh <sequencing host> 'docker exec -i sequencing-db psql -X -q -U postgres -d sequencing_api' \
--     < tools/TrainingSeeder/export-vocabulary.sql > tools/TrainingSeeder/sequencing-vocabulary.json
--
-- What leaves the server is counts, never a row:
--   values       every value of a coded column with how many rows hold it, null included, no cutoff
--   per_patient  (biobank) how many patients have k rows of each kind; per_parent is the
--   per_parent   (sequencing) same for runs per sample, samples per run and so on
--   ranges       min, median and max of a measured number, and how many rows state it
--   filled       how many rows have a value in a column whose values are never exported
--   panels       (sequencing) the panel table whole, which is the lab's vendor data, not patients'
--   run_profiles (sequencing) each distinct run configuration - instrument, kits, settings - with
--                how many runs used it, and run_sample_profiles the library preparation and analysis
--                set-up of the samples on each; equipment settings that only make sense together
-- The biobank side stays per column: its values describe patients, so none are exported jointly.
-- Identifiers, dates, birth year and month, file paths, experiment names and error texts are never
-- exported; `filled` says only how often they are present.
--
-- The repository is public: read both output files before committing them.
--
-- Read-only twice over: it runs in a READ ONLY transaction, which PostgreSQL refuses to write in,
-- and it rolls that transaction back. The output carries no timestamp, so an unchanged database
-- exports an identical file.

\set QUIET on
\set ON_ERROR_STOP on
\pset format unaligned
\pset tuples_only on

BEGIN TRANSACTION READ ONLY;

SELECT to_regclass('public.tissue_sample') IS NOT NULL AS is_biobank,
       to_regclass('public.sequencing_run') IS NOT NULL AS is_sequencing
\gset

\if :is_biobank

WITH coded(field, value) AS (
    SELECT 'patient.sex', to_jsonb("Sex") FROM patient
    UNION ALL SELECT 'patient.consent', to_jsonb("Consent") FROM patient
    UNION ALL SELECT 'patient.biobank', to_jsonb("Biobank") FROM patient
    UNION ALL SELECT 'tissue_sample.material_type', to_jsonb("MaterialType") FROM tissue_sample
    UNION ALL SELECT 'tissue_sample.diagnosis', to_jsonb("Diagnosis") FROM tissue_sample
    UNION ALL SELECT 'tissue_sample.morphology', to_jsonb("Morphology") FROM tissue_sample
    UNION ALL SELECT 'tissue_sample.p_tnm', to_jsonb("PTnm") FROM tissue_sample
    UNION ALL SELECT 'tissue_sample.retrieved', to_jsonb("Retrieved") FROM tissue_sample
    UNION ALL SELECT 'serum_sample.material_type', to_jsonb("MaterialType") FROM serum_sample
    UNION ALL SELECT 'serum_sample.diagnosis', to_jsonb("Diagnosis") FROM serum_sample
    UNION ALL SELECT 'serum_sample.retrieved', to_jsonb("Retrieved") FROM serum_sample
    UNION ALL SELECT 'genome_sample.material_type', to_jsonb("MaterialType") FROM genome_sample
    UNION ALL SELECT 'genome_sample.retrieved', to_jsonb("Retrieved") FROM genome_sample
    UNION ALL SELECT 'diagnostic_specimen.material_type', to_jsonb("MaterialType") FROM diagnostic_specimen
    UNION ALL SELECT 'diagnostic_specimen.diagnosis', to_jsonb("Diagnosis") FROM diagnostic_specimen
    UNION ALL SELECT 'diagnostic_specimen.retrieved', to_jsonb("Retrieved") FROM diagnostic_specimen
),
-- Rows of each kind per patient, a patient with none counted at 0. A sample_id can recur within a
-- patient (one row per listing of it), so rows per sample_id is counted too.
per_patient(field, k, n) AS (
    SELECT 'tissue_sample', k, count(*) FROM (
        SELECT count(s."Id") AS k FROM patient p LEFT JOIN tissue_sample s USING ("PatientId") GROUP BY p."PatientId"
    ) x GROUP BY k
    UNION ALL SELECT 'serum_sample', k, count(*) FROM (
        SELECT count(s."Id") AS k FROM patient p LEFT JOIN serum_sample s USING ("PatientId") GROUP BY p."PatientId"
    ) x GROUP BY k
    UNION ALL SELECT 'genome_sample', k, count(*) FROM (
        SELECT count(s."Id") AS k FROM patient p LEFT JOIN genome_sample s USING ("PatientId") GROUP BY p."PatientId"
    ) x GROUP BY k
    UNION ALL SELECT 'diagnostic_specimen', k, count(*) FROM (
        SELECT count(s."Id") AS k FROM patient p LEFT JOIN diagnostic_specimen s USING ("PatientId") GROUP BY p."PatientId"
    ) x GROUP BY k
    UNION ALL SELECT 'tissue_sample.rows_per_sample_id', k, count(*) FROM (
        SELECT count(*) AS k FROM tissue_sample GROUP BY "PatientId", "SampleId"
    ) x GROUP BY k
    UNION ALL SELECT 'serum_sample.rows_per_sample_id', k, count(*) FROM (
        SELECT count(*) AS k FROM serum_sample GROUP BY "PatientId", "SampleId"
    ) x GROUP BY k
    UNION ALL SELECT 'genome_sample.rows_per_sample_id', k, count(*) FROM (
        SELECT count(*) AS k FROM genome_sample GROUP BY "PatientId", "SampleId"
    ) x GROUP BY k
    UNION ALL SELECT 'diagnostic_specimen.rows_per_sample_id', k, count(*) FROM (
        SELECT count(*) AS k FROM diagnostic_specimen GROUP BY "PatientId", "SampleId"
    ) x GROUP BY k
),
measured(field, value) AS (
    SELECT 'tissue_sample.samples_no', "SamplesNo"::float8 FROM tissue_sample
    UNION ALL SELECT 'tissue_sample.available_samples_no', "AvailableSamplesNo" FROM tissue_sample
    UNION ALL SELECT 'serum_sample.samples_no', "SamplesNo" FROM serum_sample
    UNION ALL SELECT 'serum_sample.available_samples_no', "AvailableSamplesNo" FROM serum_sample
    UNION ALL SELECT 'genome_sample.samples_no', "SamplesNo" FROM genome_sample
    UNION ALL SELECT 'genome_sample.available_samples_no', "AvailableSamplesNo" FROM genome_sample
),
-- Presence only: these columns' values are identifiers or dates and stay on the server.
filled(field, filled, total) AS (
    SELECT 'patient.birth_year', count("BirthYear"), count(*) FROM patient
    UNION ALL SELECT 'patient.birth_month', count("BirthMonth"), count(*) FROM patient
    UNION ALL SELECT 'patient.accession_numbers', count(*) FILTER (WHERE "AccessionNumbers" <> '[]'), count(*) FROM patient
    UNION ALL SELECT 'tissue_sample.predictive_number', count("PredictiveNumber"), count(*) FROM tissue_sample
    UNION ALL SELECT 'tissue_sample.biopsy', count("Biopsy"), count(*) FROM tissue_sample
    UNION ALL SELECT 'tissue_sample.accession_numbers', count(*) FILTER (WHERE "AccessionNumbers" <> '[]'), count(*) FROM tissue_sample
    UNION ALL SELECT 'tissue_sample.event_number', count("EventNumber"), count(*) FROM tissue_sample
    UNION ALL SELECT 'tissue_sample.collection_year', count("CollectionYear"), count(*) FROM tissue_sample
    UNION ALL SELECT 'tissue_sample.cut_time', count("CutTime"), count(*) FROM tissue_sample
    UNION ALL SELECT 'tissue_sample.freeze_time', count("FreezeTime"), count(*) FROM tissue_sample
    UNION ALL SELECT 'serum_sample.predictive_number', count("PredictiveNumber"), count(*) FROM serum_sample
    UNION ALL SELECT 'serum_sample.biopsy', count("Biopsy"), count(*) FROM serum_sample
    UNION ALL SELECT 'serum_sample.accession_numbers', count(*) FILTER (WHERE "AccessionNumbers" <> '[]'), count(*) FROM serum_sample
    UNION ALL SELECT 'serum_sample.event_number', count("EventNumber"), count(*) FROM serum_sample
    UNION ALL SELECT 'serum_sample.collection_year', count("CollectionYear"), count(*) FROM serum_sample
    UNION ALL SELECT 'serum_sample.taking_date', count("TakingDate"), count(*) FROM serum_sample
    UNION ALL SELECT 'genome_sample.predictive_number', count("PredictiveNumber"), count(*) FROM genome_sample
    UNION ALL SELECT 'genome_sample.biopsy', count("Biopsy"), count(*) FROM genome_sample
    UNION ALL SELECT 'genome_sample.accession_numbers', count(*) FILTER (WHERE "AccessionNumbers" <> '[]'), count(*) FROM genome_sample
    UNION ALL SELECT 'genome_sample.event_number', count("EventNumber"), count(*) FROM genome_sample
    UNION ALL SELECT 'genome_sample.collection_year', count("CollectionYear"), count(*) FROM genome_sample
    UNION ALL SELECT 'genome_sample.taking_date', count("TakingDate"), count(*) FROM genome_sample
    UNION ALL SELECT 'diagnostic_specimen.specimen_number', count("SpecimenNumber"), count(*) FROM diagnostic_specimen
    UNION ALL SELECT 'diagnostic_specimen.year', count("Year"), count(*) FROM diagnostic_specimen
    UNION ALL SELECT 'diagnostic_specimen.taking_date', count("TakingDate"), count(*) FROM diagnostic_specimen
)
SELECT jsonb_pretty(jsonb_build_object(
    'source', 'biobank',
    'schema', (SELECT max("MigrationId") FROM "__EFMigrationsHistory"),
    'patients', (SELECT count(*) FROM patient),
    'values', (
        SELECT coalesce(jsonb_object_agg(field, counts), '{}') FROM (
            SELECT field, jsonb_agg(jsonb_build_object('value', value, 'count', n) ORDER BY n DESC, value) AS counts
            FROM (SELECT field, value, count(*) AS n FROM coded GROUP BY field, value) c
            GROUP BY field
        ) f
    ),
    'per_patient', (
        SELECT coalesce(jsonb_object_agg(field, counts), '{}') FROM (
            SELECT field, jsonb_agg(jsonb_build_object('value', k, 'count', n) ORDER BY k) AS counts
            FROM per_patient GROUP BY field
        ) f
    ),
    'ranges', (
        SELECT coalesce(jsonb_object_agg(field, stats), '{}') FROM (
            SELECT field, jsonb_build_object(
                'min', min(value),
                'median', percentile_cont(0.5) WITHIN GROUP (ORDER BY value),
                'max', max(value),
                'filled', count(value),
                'total', count(*)) AS stats
            FROM measured GROUP BY field
        ) f
    ),
    'filled', (
        SELECT coalesce(jsonb_object_agg(field, jsonb_build_object('filled', filled, 'total', total)), '{}')
        FROM filled
    )
));

\elif :is_sequencing

-- The read structure as one value per run, e.g. "151,8i,8i,151" (i marks an index read).
WITH read_structure AS (
    SELECT "RunId", string_agg("NumCycles"::text || CASE WHEN "IsIndexedRead" THEN 'i' ELSE '' END, ',' ORDER BY "Position") AS structure
    FROM run_read GROUP BY "RunId"
),
-- A run's whole configuration: instrument, kits and settings, which only make sense together (a
-- NextSeq has four lanes and an NB serial). Lab equipment, not patient data; which statistics the
-- run states is kept as flags, the numbers themselves are in `ranges`.
run_config AS (
    SELECT r."RunId", jsonb_build_object(
        'platform', r."Platform",
        'instrument_model', r."InstrumentModel",
        'instrument_id', r."InstrumentId",
        'source_class', r."SourceClass",
        'chemistry', r."Chemistry",
        'reagent_kit', r."ReagentKit",
        'workflow', r."Workflow",
        'assay', r."Assay",
        'lane_count', r."LaneCount",
        'completion_status', r."CompletionStatus",
        'read_structure', rs.structure,
        'mamma_print_name', coalesce(upper(r."ExperimentName") LIKE 'MP\_%', false),
        'has_percentage_q30', r."PercentageQ30" IS NOT NULL,
        'has_cluster_count', r."ClusterCountPassingFilter" IS NOT NULL,
        'has_cluster_stats', r."ClusterDensity" IS NOT NULL) AS profile
    FROM sequencing_run r LEFT JOIN read_structure rs USING ("RunId")
),
run_profile AS (
    SELECT row_number() OVER (ORDER BY count(*) DESC, profile::text) AS id, profile, count(*) AS n
    FROM run_config GROUP BY profile
),
-- What one sample got on one run: its library preparation, its analysis and that analysis's output
-- files (role and format only, never a path), and how many read files it has. Tied to the run's
-- configuration by id, because a kit belongs to an instrument (TSO500 runs on the NextSeq).
run_sample_profile AS (
    SELECT p.id AS run_profile, jsonb_build_object(
        'sample_type', rs."SampleType",
        'lane_count', rs."LaneCount",
        'fastq_files', (SELECT count(*) FROM sequencing_file f WHERE f."RunSampleId" = rs."Id"),
        'fastq_without_lane', (SELECT count(*) FROM sequencing_file f WHERE f."RunSampleId" = rs."Id" AND f."Lane" IS NULL),
        'library', CASE WHEN lp."RunSampleId" IS NULL THEN NULL ELSE jsonb_build_object(
            'library_prep_kit', lp."LibraryPrepKit",
            'target_enrichment_kit', lp."TargetEnrichmentKit",
            'panel_id', lp."PanelId",
            'pcr_free', lp."PcrFree",
            'umi_present', lp."UmiPresent",
            'intended_insert_size', lp."IntendedInsertSize",
            'intended_read_length', lp."IntendedReadLength",
            'has_input_amount', lp."InputAmount" IS NOT NULL) END,
        'analysis', CASE WHEN a."Id" IS NULL THEN NULL ELSE jsonb_build_object(
            'analysis_type', a."AnalysisType",
            'pipeline_name', a."PipelineName",
            'reference_genome', a."ReferenceGenome",
            'has_quality_metrics', q."AnalysisId" IS NOT NULL,
            'files', (
                SELECT coalesce(jsonb_agg(jsonb_build_object('role', f."Role", 'format', f."Format") ORDER BY f."Role", f."Format"), '[]')
                FROM sequencing_file f WHERE f."AnalysisId" = a."Id")) END) AS profile
    FROM run_sample rs
    JOIN run_config c USING ("RunId")
    JOIN run_profile p ON p.profile = c.profile
    LEFT JOIN library_preparation lp ON lp."RunSampleId" = rs."Id"
    LEFT JOIN analysis a ON a."RunSampleId" = rs."Id"
    LEFT JOIN quality_metrics q ON q."AnalysisId" = a."Id"
),
coded(field, value) AS (
    SELECT 'sample.id_scheme', to_jsonb("IdScheme") FROM sample
    UNION ALL SELECT 'run_sample.sample_type', to_jsonb("SampleType") FROM run_sample
    UNION ALL SELECT 'run_sample.lane_count', to_jsonb("LaneCount") FROM run_sample
    UNION ALL SELECT 'library_preparation.library_prep_kit', to_jsonb("LibraryPrepKit") FROM library_preparation
    UNION ALL SELECT 'library_preparation.target_enrichment_kit', to_jsonb("TargetEnrichmentKit") FROM library_preparation
    UNION ALL SELECT 'library_preparation.panel_id', to_jsonb("PanelId") FROM library_preparation
    UNION ALL SELECT 'library_preparation.pcr_free', to_jsonb("PcrFree") FROM library_preparation
    UNION ALL SELECT 'library_preparation.umi_present', to_jsonb("UmiPresent") FROM library_preparation
    UNION ALL SELECT 'library_preparation.intended_read_length', to_jsonb("IntendedReadLength") FROM library_preparation
    UNION ALL SELECT 'library_preparation.intended_insert_size', to_jsonb("IntendedInsertSize") FROM library_preparation
    UNION ALL SELECT 'analysis.analysis_type', to_jsonb("AnalysisType") FROM analysis
    UNION ALL SELECT 'analysis.pipeline_name', to_jsonb("PipelineName") FROM analysis
    UNION ALL SELECT 'analysis.reference_genome', to_jsonb("ReferenceGenome") FROM analysis
    UNION ALL SELECT 'sequencing_file.role', to_jsonb("Role") FROM sequencing_file
    UNION ALL SELECT 'sequencing_file.format', to_jsonb("Format") FROM sequencing_file
    UNION ALL SELECT 'sequencing_run.source_class', to_jsonb("SourceClass") FROM sequencing_run
    UNION ALL SELECT 'sequencing_run.platform', to_jsonb("Platform") FROM sequencing_run
    UNION ALL SELECT 'sequencing_run.instrument_model', to_jsonb("InstrumentModel") FROM sequencing_run
    UNION ALL SELECT 'sequencing_run.instrument_id', to_jsonb("InstrumentId") FROM sequencing_run
    UNION ALL SELECT 'sequencing_run.chemistry', to_jsonb("Chemistry") FROM sequencing_run
    UNION ALL SELECT 'sequencing_run.reagent_kit', to_jsonb("ReagentKit") FROM sequencing_run
    UNION ALL SELECT 'sequencing_run.workflow', to_jsonb("Workflow") FROM sequencing_run
    UNION ALL SELECT 'sequencing_run.assay', to_jsonb("Assay") FROM sequencing_run
    UNION ALL SELECT 'sequencing_run.completion_status', to_jsonb("CompletionStatus") FROM sequencing_run
    UNION ALL SELECT 'sequencing_run.lane_count', to_jsonb("LaneCount") FROM sequencing_run
    UNION ALL SELECT 'sequencing_run.read_structure', to_jsonb(structure) FROM read_structure
),
per_parent(field, k, n) AS (
    -- Runs a sample was sequenced in; above 1 is a resequenced sample.
    SELECT 'sample.run_samples', k, count(*) FROM (
        SELECT count(rs."Id") AS k FROM sample s LEFT JOIN run_sample rs ON rs."SampleExternalId" = s."ExternalId" GROUP BY s."ExternalId"
    ) x GROUP BY k
    UNION ALL SELECT 'sequencing_run.run_samples', k, count(*) FROM (
        SELECT count(rs."Id") AS k FROM sequencing_run r LEFT JOIN run_sample rs USING ("RunId") GROUP BY r."RunId"
    ) x GROUP BY k
    UNION ALL SELECT 'run_sample.analyses', k, count(*) FROM (
        SELECT count(a."Id") AS k FROM run_sample rs LEFT JOIN analysis a ON a."RunSampleId" = rs."Id" GROUP BY rs."Id"
    ) x GROUP BY k
    UNION ALL SELECT 'run_sample.files', k, count(*) FROM (
        SELECT count(f."Id") AS k FROM run_sample rs LEFT JOIN sequencing_file f ON f."RunSampleId" = rs."Id" GROUP BY rs."Id"
    ) x GROUP BY k
    UNION ALL SELECT 'analysis.files', k, count(*) FROM (
        SELECT count(f."Id") AS k FROM analysis a LEFT JOIN sequencing_file f ON f."AnalysisId" = a."Id" GROUP BY a."Id"
    ) x GROUP BY k
    -- Sample folders sharing one predictive number: a number belongs to a case, not a sample.
    UNION ALL SELECT 'predictive_key.samples', k, count(*) FROM (
        SELECT count(*) AS k FROM sample WHERE "PredictiveKey" IS NOT NULL GROUP BY "PredictiveKey"
    ) x GROUP BY k
),
measured(field, value) AS (
    SELECT 'quality_metrics.median_read_depth', "MedianReadDepth" FROM quality_metrics
    UNION ALL SELECT 'quality_metrics.observed_read_length', "ObservedReadLength" FROM quality_metrics
    UNION ALL SELECT 'library_preparation.input_amount', "InputAmount" FROM library_preparation
    UNION ALL SELECT 'sequencing_run.percentage_q30', "PercentageQ30" FROM sequencing_run
    UNION ALL SELECT 'sequencing_run.cluster_density', "ClusterDensity" FROM sequencing_run
    UNION ALL SELECT 'sequencing_run.percentage_clusters_passing_filter', "PercentageClustersPassingFilter" FROM sequencing_run
    UNION ALL SELECT 'sequencing_run.cluster_count_passing_filter', "ClusterCountPassingFilter" FROM sequencing_run
    UNION ALL SELECT 'sequencing_run.estimated_yield', "EstimatedYield" FROM sequencing_run
),
-- Presence only: these columns' values are identifiers, dates, paths or free text.
filled(field, filled, total) AS (
    SELECT 'sample.predictive_number', count("PredictiveNumber"), count(*) FROM sample
    UNION ALL SELECT 'run_sample.sample_index', count("SampleIndex"), count(*) FROM run_sample
    UNION ALL SELECT 'sequencing_file.checksum', count("Checksum"), count(*) FROM sequencing_file
    UNION ALL SELECT 'sequencing_file.size_bytes', count("SizeBytes"), count(*) FROM sequencing_file
    UNION ALL SELECT 'sequencing_file.lane', count("Lane"), count(*) FROM sequencing_file
    UNION ALL SELECT 'sequencing_file.read', count("Read"), count(*) FROM sequencing_file
    UNION ALL SELECT 'sequencing_run.experiment_name', count("ExperimentName"), count(*) FROM sequencing_run
    UNION ALL SELECT 'sequencing_run.flowcell_id', count("FlowcellId"), count(*) FROM sequencing_run
    UNION ALL SELECT 'sequencing_run.run_number', count("RunNumber"), count(*) FROM sequencing_run
    UNION ALL SELECT 'sequencing_run.run_date', count("RunDate"), count(*) FROM sequencing_run
    UNION ALL SELECT 'sequencing_run.error_description', count("ErrorDescription"), count(*) FROM sequencing_run
    UNION ALL SELECT 'run_sample.library_preparation', count(lp."RunSampleId"), count(*)
        FROM run_sample rs LEFT JOIN library_preparation lp ON lp."RunSampleId" = rs."Id"
    UNION ALL SELECT 'analysis.quality_metrics', count(q."AnalysisId"), count(*)
        FROM analysis a LEFT JOIN quality_metrics q ON q."AnalysisId" = a."Id"
)
SELECT jsonb_pretty(jsonb_build_object(
    'source', 'sequencing',
    'schema', (SELECT max("MigrationId") FROM "__EFMigrationsHistory"),
    'samples', (SELECT count(*) FROM sample),
    'runs', (SELECT count(*) FROM sequencing_run),
    'values', (
        SELECT coalesce(jsonb_object_agg(field, counts), '{}') FROM (
            SELECT field, jsonb_agg(jsonb_build_object('value', value, 'count', n) ORDER BY n DESC, value) AS counts
            FROM (SELECT field, value, count(*) AS n FROM coded GROUP BY field, value) c
            GROUP BY field
        ) f
    ),
    'per_parent', (
        SELECT coalesce(jsonb_object_agg(field, counts), '{}') FROM (
            SELECT field, jsonb_agg(jsonb_build_object('value', k, 'count', n) ORDER BY k) AS counts
            FROM per_parent GROUP BY field
        ) f
    ),
    'ranges', (
        SELECT coalesce(jsonb_object_agg(field, stats), '{}') FROM (
            SELECT field, jsonb_build_object(
                'min', min(value),
                'median', percentile_cont(0.5) WITHIN GROUP (ORDER BY value),
                'max', max(value),
                'filled', count(value),
                'total', count(*)) AS stats
            FROM measured GROUP BY field
        ) f
    ),
    'filled', (
        SELECT coalesce(jsonb_object_agg(field, jsonb_build_object('filled', filled, 'total', total)), '{}')
        FROM filled
    ),
    -- How the uploader recognises a MammaPrint run (SequencingMapper.IsMammaPrint), counted both ways.
    'mamma_print', (
        SELECT jsonb_build_object(
            'by_source_class', count(*) FILTER (WHERE lower("SourceClass") = 'mamma-print'),
            'by_experiment_name', count(*) FILTER (WHERE upper("ExperimentName") LIKE 'MP\_%'),
            'either', count(*) FILTER (WHERE lower("SourceClass") = 'mamma-print' OR upper("ExperimentName") LIKE 'MP\_%'),
            'runs', count(*))
        FROM sequencing_run
    ),
    'run_profiles', (
        SELECT coalesce(jsonb_agg(jsonb_build_object('id', id, 'count', n, 'profile', profile) ORDER BY id), '[]')
        FROM run_profile
    ),
    'run_sample_profiles', (
        SELECT coalesce(jsonb_agg(jsonb_build_object('run_profile', run_profile, 'count', n, 'profile', profile)
            ORDER BY run_profile, n DESC, profile::text), '[]')
        FROM (SELECT run_profile, profile, count(*) AS n FROM run_sample_profile GROUP BY run_profile, profile) x
    ),
    -- Whole, except target_regions_ref, which is a file reference.
    'panels', (
        SELECT coalesce(jsonb_agg(jsonb_build_object(
            'panel_id', "PanelId",
            'name', "Name",
            'abbreviation', "Abbreviation",
            'vendor', "Vendor",
            'assay', "Assay",
            'catalogue_code', "CatalogueCode",
            'available_from', "AvailableFrom",
            'available_to', "AvailableTo",
            'genes', "Genes"::jsonb) ORDER BY "PanelId"), '[]')
        FROM panel
    )
));

\else

DO $$ BEGIN
    RAISE EXCEPTION 'Neither a biobank_api nor a sequencing_api database: no tissue_sample or sequencing_run table.';
END $$;

\endif

ROLLBACK;
