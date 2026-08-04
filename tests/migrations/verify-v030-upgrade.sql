\set ON_ERROR_STOP on

DO $verification$
DECLARE
    legacy_theme_column_count integer;
    obsolete_schedule_column_count integer;
    applied_migration_count integer;
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM public.theme_config
        WHERE id_config = 1
          AND font_color = '#112233'
          AND background_color = '#445566'
          AND background_image_opacity = 40
    ) THEN
        RAISE EXCEPTION 'Theme data was not preserved during the upgrade';
    END IF;

    SELECT COUNT(*)
    INTO legacy_theme_column_count
    FROM information_schema.columns
    WHERE table_schema = 'public'
      AND table_name IN ('theme_config', 'theme_config_l')
      AND column_name IN (
          'gradient_enabled',
          'gradient_start_color',
          'gradient_end_color',
          'background_image_data_url',
          'soft_lighten_percent',
          'button_strong_darken_percent'
      );

    IF legacy_theme_column_count <> 0 THEN
        RAISE EXCEPTION 'Legacy theme columns remain after the upgrade';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'auto_creation_config'
          AND column_name = 'reporting_period'
    ) OR NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'auto_creation_config'
          AND column_name = 'reporting_offset_business_days'
    ) THEN
        RAISE EXCEPTION 'Reporting-period columns were not created';
    END IF;

    SELECT COUNT(*)
    INTO obsolete_schedule_column_count
    FROM information_schema.columns
    WHERE table_schema = 'public'
      AND table_name IN ('auto_creation_config', 'auto_creation_config_l')
      AND column_name IN ('id_creation_day', 'id_begin_day');

    IF obsolete_schedule_column_count <> 0 THEN
        RAISE EXCEPTION 'Obsolete weekday schedule columns remain after the upgrade';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'answer'
          AND column_name = 'date_update'
    ) OR EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'auto_creation_config'
          AND column_name IN ('date_update', 'user_update')
    ) THEN
        RAISE EXCEPTION 'Update metadata columns are inconsistent';
    END IF;

    SELECT COUNT(*)
    INTO applied_migration_count
    FROM public.schema_migrations
    WHERE version IN ('028', '029', '030');

    IF applied_migration_count <> 3 THEN
        RAISE EXCEPTION 'Not all current migrations were applied';
    END IF;
END;
$verification$;
