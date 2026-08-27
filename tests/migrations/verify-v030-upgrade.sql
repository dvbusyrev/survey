\set ON_ERROR_STOP on

UPDATE public.email_config
SET subject_text = subject_text
WHERE id_config = 1;

DO $verification$
DECLARE
    legacy_theme_column_count integer;
    obsolete_schedule_column_count integer;
    applied_migration_count integer;
    audit_column_without_generator_count integer;
    email_config_count integer;
    redundant_user_update_column_count integer;
    redundant_date_update_column_count integer;
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

    IF to_regclass('public.week_day') IS NOT NULL THEN
        RAISE EXCEPTION 'Obsolete week_day table remains after the upgrade';
    END IF;

    SELECT COUNT(*)
    INTO redundant_date_update_column_count
    FROM information_schema.columns column_definition
    INNER JOIN information_schema.tables table_definition
        ON table_definition.table_schema = column_definition.table_schema
       AND table_definition.table_name = column_definition.table_name
    WHERE column_definition.table_schema = 'public'
      AND column_definition.column_name = 'date_update'
      AND table_definition.table_type = 'BASE TABLE';

    IF redundant_date_update_column_count <> 0 THEN
        RAISE EXCEPTION 'Redundant date_update columns remain after the upgrade';
    END IF;

    SELECT COUNT(*)
    INTO redundant_user_update_column_count
    FROM information_schema.columns
    WHERE table_schema = 'public'
      AND column_name = 'user_update';

    IF redundant_user_update_column_count <> 0 THEN
        RAISE EXCEPTION 'Redundant user_update columns remain after the upgrade';
    END IF;

    SELECT COUNT(*)
    INTO email_config_count
    FROM public.email_config
    WHERE id_config = 1;

    IF email_config_count <> 1
       OR (SELECT COUNT(*) FROM public.email_config) <> 1 THEN
        RAISE EXCEPTION 'Email settings were not converted to a singleton';
    END IF;

    SELECT COUNT(*)
    INTO audit_column_without_generator_count
    FROM information_schema.columns
    WHERE table_schema = 'public'
      AND table_name IN (
          'answer_l',
          'answer_item_l',
          'app_user_l',
          'auto_creation_config_l',
          'email_config_l',
          'organization_l',
          'organization_survey_l',
          'organization_survey_template_l',
          'survey_l',
          'survey_question_l',
          'survey_template_auto_creation_config_l',
          'survey_template_l',
          'survey_template_question_l',
          'theme_config_l'
      )
      AND column_name = 'id_audit'
      AND is_identity = 'NO'
      AND column_default IS NULL;

    IF audit_column_without_generator_count <> 0 THEN
        RAISE EXCEPTION 'An audit id generator is missing after the upgrade';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM public.email_config_l
        WHERE operation = 'UPDATE'
          AND id_config = 1
    ) THEN
        RAISE EXCEPTION 'Email settings audit update failed after the upgrade';
    END IF;

    IF to_regclass('public.survey_template') IS NULL
       OR to_regclass('public.survey_template_question') IS NULL
       OR to_regclass('public.organization_survey_template') IS NULL
       OR to_regclass('public.survey_template_auto_creation_config') IS NULL
       OR to_regclass('public.survey_auto_creation_config') IS NOT NULL THEN
        RAISE EXCEPTION 'Survey template schema is inconsistent after the upgrade';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'survey_template'
          AND column_name = 'ancestor_id'
    ) THEN
        RAISE EXCEPTION 'Planned survey template ancestry was not created';
    END IF;

    SELECT COUNT(*)
    INTO applied_migration_count
    FROM public.schema_migrations
    WHERE version IN (
        '028', '029', '030', '031', '032', '033', '034',
        '035', '036', '037', '038', '039', '040', '041',
        '042', '043', '044', '045', '046', '047', '048',
        '049', '050'
    );

    IF applied_migration_count <> 23 THEN
        RAISE EXCEPTION 'Not all current migrations were applied';
    END IF;
END;
$verification$;
