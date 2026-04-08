using Npgsql;

namespace main_project.Infrastructure.Database;

public static class SingularTableNamingBootstrapper
{
    private static readonly (string OldName, string NewName)[] RelationRenames =
    {
        ("public.omsu", "public.organization"),
        ("public.organizations", "public.organization"),
        ("public.omsu_surveys", "public.organization_survey"),
        ("public.organization_surveys", "public.organization_survey"),
        ("public.users", "public.app_user"),
        ("public.surveys", "public.survey"),
        ("public.survey_questions", "public.survey_question"),
        ("public.history_surveys", "public.history_survey"),
        ("public.history_survey_questions", "public.history_survey_question"),
        ("public.history_answer", "public.answer"),
        ("public.history_answer_items", "public.answer_item"),
        ("public.history_answer_item", "public.answer_item"),
        ("public.access_extensions", "public.access_extension"),
        ("public.logs", "public.log"),
        ("public.omsu_l", "public.organization_l"),
        ("public.organizations_l", "public.organization_l"),
        ("public.omsu_surveys_l", "public.organization_survey_l"),
        ("public.organization_surveys_l", "public.organization_survey_l"),
        ("public.users_l", "public.app_user_l"),
        ("public.surveys_l", "public.survey_l"),
        ("public.history_answer_l", "public.answer_l")
    };

    private static readonly (string TableName, string OldName, string NewName)[] ConstraintRenames =
    {
        ("public.organization", "omsu_pkey", "organization_pkey"),
        ("public.organization", "organizations_pkey", "organization_pkey"),
        ("public.organization", "omsu_block_not_null", "organization_block_not_null"),
        ("public.organization", "omsu_id_omsu_not_null", "organization_organization_id_not_null"),
        ("public.organization", "omsu_name_omsu_not_null", "organization_organization_name_not_null"),
        ("public.organization_survey", "omsu_surveys_pkey", "organization_survey_pkey"),
        ("public.organization_survey", "pk_omsu_surveys", "organization_survey_pkey"),
        ("public.organization_survey", "organization_surveys_pkey", "organization_survey_pkey"),
        ("public.organization_survey", "omsu_surveys_id_omsu_fkey", "organization_survey_organization_id_fkey"),
        ("public.organization_survey", "fk_omsu_surveys_omsu", "organization_survey_organization_id_fkey"),
        ("public.organization_survey", "organization_surveys_organization_id_fkey", "organization_survey_organization_id_fkey"),
        ("public.organization_survey", "organization_surveys_id_omsu_fkey", "organization_survey_organization_id_fkey"),
        ("public.organization_survey", "fk_organization_survey_organization", "organization_survey_organization_id_fkey"),
        ("public.organization_survey", "omsu_surveys_id_omsu_not_null", "organization_survey_organization_id_not_null"),
        ("public.organization_survey", "omsu_surveys_id_survey_not_null", "organization_survey_id_survey_not_null"),
        ("public.app_user", "users_pkey", "app_user_pkey"),
        ("public.app_user", "users_name_user_key", "app_user_name_user_key"),
        ("public.app_user", "users_id_omsu_fkey", "app_user_organization_id_fkey"),
        ("public.app_user", "users_organization_id_fkey", "app_user_organization_id_fkey"),
        ("public.app_user", "chk_users_name_role", "chk_app_user_name_role"),
        ("public.app_user", "users_full_name_not_null", "app_user_full_name_not_null"),
        ("public.app_user", "users_hash_password_not_null", "app_user_hash_password_not_null"),
        ("public.app_user", "users_id_user_not_null", "app_user_id_user_not_null"),
        ("public.app_user", "users_name_role_not_null", "app_user_name_role_not_null"),
        ("public.app_user", "users_name_user_not_null", "app_user_name_user_not_null"),
        ("public.survey", "surveys_pkey", "survey_pkey"),
        ("public.survey", "surveys_date_close_not_null", "survey_date_close_not_null"),
        ("public.survey", "surveys_date_create_not_null", "survey_date_create_not_null"),
        ("public.survey", "surveys_date_open_not_null", "survey_date_open_not_null"),
        ("public.survey", "surveys_id_survey_not_null", "survey_id_survey_not_null"),
        ("public.survey", "surveys_name_survey_not_null", "survey_name_survey_not_null"),
        ("public.survey_question", "survey_questions_pkey", "survey_question_pkey"),
        ("public.survey_question", "fk_survey_questions_survey", "survey_question_id_survey_fkey"),
        ("public.survey_question", "uq_survey_questions_order", "survey_question_id_survey_question_order_key"),
        ("public.survey_question", "survey_questions_created_at_not_null", "survey_question_created_at_not_null"),
        ("public.survey_question", "survey_questions_id_question_not_null", "survey_question_id_question_not_null"),
        ("public.survey_question", "survey_questions_id_survey_not_null", "survey_question_id_survey_not_null"),
        ("public.survey_question", "survey_questions_question_order_not_null", "survey_question_question_order_not_null"),
        ("public.survey_question", "survey_questions_question_text_not_null", "survey_question_question_text_not_null"),
        ("public.history_survey", "history_surveys_pkey", "history_survey_pkey"),
        ("public.history_survey", "history_surveys_id_survey_key", "history_survey_id_survey_key"),
        ("public.history_survey", "history_surveys_date_begin_not_null", "history_survey_date_begin_not_null"),
        ("public.history_survey", "history_surveys_date_end_not_null", "history_survey_date_end_not_null"),
        ("public.history_survey", "history_surveys_id_hsurvey_not_null", "history_survey_id_hsurvey_not_null"),
        ("public.history_survey", "history_surveys_id_survey_not_null", "history_survey_id_survey_not_null"),
        ("public.history_survey", "history_surveys_name_survey_not_null", "history_survey_name_survey_not_null"),
        ("public.history_survey_question", "history_survey_questions_pkey", "history_survey_question_pkey"),
        ("public.history_survey_question", "uq_history_survey_questions_order", "history_survey_question_id_survey_question_order_key"),
        ("public.history_survey_question", "history_survey_questions_created_at_not_null", "history_survey_question_created_at_not_null"),
        ("public.history_survey_question", "history_survey_questions_id_history_question_not_null", "history_survey_question_id_history_question_not_null"),
        ("public.history_survey_question", "history_survey_questions_id_survey_not_null", "history_survey_question_id_survey_not_null"),
        ("public.history_survey_question", "history_survey_questions_question_order_not_null", "history_survey_question_question_order_not_null"),
        ("public.history_survey_question", "history_survey_questions_question_text_not_null", "history_survey_question_question_text_not_null"),
        ("public.answer", "history_answer_id_omsu_fkey", "answer_organization_id_fkey"),
        ("public.answer", "history_answer_omsu_survey_key", "answer_organization_survey_key"),
        ("public.answer", "history_answer_id_omsu_not_null", "answer_organization_id_not_null"),
        ("public.answer", "history_answer_id_answer_not_null", "answer_id_answer_not_null"),
        ("public.answer", "history_answer_id_survey_not_null", "answer_id_survey_not_null"),
        ("public.answer", "history_answer_pkey", "answer_pkey"),
        ("public.answer_item", "history_answer_items_pkey", "answer_item_pkey"),
        ("public.answer_item", "fk_history_answer_items_answer", "answer_item_id_answer_fkey"),
        ("public.answer_item", "uq_history_answer_items_order", "answer_item_id_answer_question_order_key"),
        ("public.answer_item", "chk_history_answer_items_rating", "chk_answer_item_rating"),
        ("public.answer_item", "history_answer_items_id_answer_not_null", "answer_item_id_answer_not_null"),
        ("public.answer_item", "history_answer_items_id_item_not_null", "answer_item_id_item_not_null"),
        ("public.answer_item", "history_answer_items_question_order_not_null", "answer_item_question_order_not_null"),
        ("public.answer_item", "history_answer_items_question_text_not_null", "answer_item_question_text_not_null"),
        ("public.access_extension", "access_extensions_pkey", "access_extension_pkey"),
        ("public.access_extension", "access_extensions_id_omsu_fkey", "access_extension_organization_id_fkey"),
        ("public.access_extension", "access_extensions_organization_id_fkey", "access_extension_organization_id_fkey"),
        ("public.access_extension", "access_extensions_id_survey_fkey", "access_extension_id_survey_fkey"),
        ("public.access_extension", "access_extensions_created_at_not_null", "access_extension_created_at_not_null"),
        ("public.access_extension", "access_extensions_id_not_null", "access_extension_id_not_null"),
        ("public.access_extension", "access_extensions_id_omsu_not_null", "access_extension_organization_id_not_null"),
        ("public.access_extension", "access_extensions_id_survey_not_null", "access_extension_id_survey_not_null"),
        ("public.access_extension", "access_extensions_new_end_date_not_null", "access_extension_new_end_date_not_null"),
        ("public.log", "logs_pkey", "log_pkey"),
        ("public.log", "logs_date_not_null", "log_date_not_null"),
        ("public.log", "logs_description_not_null", "log_description_not_null"),
        ("public.log", "logs_event_type_not_null", "log_event_type_not_null"),
        ("public.log", "logs_id_log_not_null", "log_id_log_not_null"),
        ("public.log", "logs_id_user_not_null", "log_id_user_not_null"),
        ("public.log", "logs_target_type_not_null", "log_target_type_not_null"),
        ("public.organization_l", "omsu_l_pkey", "organization_l_pkey"),
        ("public.organization_l", "organizations_l_pkey", "organization_l_pkey"),
        ("public.organization_l", "omsu_l_changed_at_not_null", "organization_l_changed_at_not_null"),
        ("public.organization_l", "omsu_l_id_audit_not_null", "organization_l_id_audit_not_null"),
        ("public.organization_l", "omsu_l_operation_not_null", "organization_l_operation_not_null"),
        ("public.organization_l", "omsu_l_record_pk_not_null", "organization_l_record_pk_not_null"),
        ("public.organization_l", "omsu_l_row_data_not_null", "organization_l_row_data_not_null"),
        ("public.organization_survey_l", "omsu_surveys_l_pkey", "organization_survey_l_pkey"),
        ("public.organization_survey_l", "organization_surveys_l_pkey", "organization_survey_l_pkey"),
        ("public.organization_survey_l", "omsu_surveys_l_changed_at_not_null", "organization_survey_l_changed_at_not_null"),
        ("public.organization_survey_l", "omsu_surveys_l_id_audit_not_null", "organization_survey_l_id_audit_not_null"),
        ("public.organization_survey_l", "omsu_surveys_l_operation_not_null", "organization_survey_l_operation_not_null"),
        ("public.organization_survey_l", "omsu_surveys_l_record_pk_not_null", "organization_survey_l_record_pk_not_null"),
        ("public.organization_survey_l", "omsu_surveys_l_row_data_not_null", "organization_survey_l_row_data_not_null"),
        ("public.app_user_l", "users_l_pkey", "app_user_l_pkey"),
        ("public.app_user_l", "users_l_changed_at_not_null", "app_user_l_changed_at_not_null"),
        ("public.app_user_l", "users_l_id_audit_not_null", "app_user_l_id_audit_not_null"),
        ("public.app_user_l", "users_l_operation_not_null", "app_user_l_operation_not_null"),
        ("public.app_user_l", "users_l_record_pk_not_null", "app_user_l_record_pk_not_null"),
        ("public.app_user_l", "users_l_row_data_not_null", "app_user_l_row_data_not_null"),
        ("public.survey_l", "surveys_l_pkey", "survey_l_pkey"),
        ("public.survey_l", "surveys_l_changed_at_not_null", "survey_l_changed_at_not_null"),
        ("public.survey_l", "surveys_l_id_audit_not_null", "survey_l_id_audit_not_null"),
        ("public.survey_l", "surveys_l_operation_not_null", "survey_l_operation_not_null"),
        ("public.survey_l", "surveys_l_record_pk_not_null", "survey_l_record_pk_not_null"),
        ("public.survey_l", "surveys_l_row_data_not_null", "survey_l_row_data_not_null"),
        ("public.answer_l", "history_answer_l_pkey", "answer_l_pkey"),
        ("public.answer_l", "history_answer_l_changed_at_not_null", "answer_l_changed_at_not_null"),
        ("public.answer_l", "history_answer_l_id_audit_not_null", "answer_l_id_audit_not_null"),
        ("public.answer_l", "history_answer_l_operation_not_null", "answer_l_operation_not_null"),
        ("public.answer_l", "history_answer_l_record_pk_not_null", "answer_l_record_pk_not_null"),
        ("public.answer_l", "history_answer_l_row_data_not_null", "answer_l_row_data_not_null")
    };

    private static readonly (string OldName, string NewName)[] IndexRenames =
    {
        ("public.idx_omsu_block", "public.idx_organization_block"),
        ("public.idx_organizations_block", "public.idx_organization_block"),
        ("public.idx_omsu_date_end", "public.idx_organization_date_end"),
        ("public.idx_organizations_date_end", "public.idx_organization_date_end"),
        ("public.idx_omsu_surveys_id_survey", "public.idx_organization_survey_id_survey"),
        ("public.idx_organization_surveys_id_survey", "public.idx_organization_survey_id_survey"),
        ("public.idx_users_id_omsu", "public.idx_app_user_organization_id"),
        ("public.idx_users_organization_id", "public.idx_app_user_organization_id"),
        ("public.idx_users_date_end", "public.idx_app_user_date_end"),
        ("public.idx_surveys_date_close", "public.idx_survey_date_close"),
        ("public.idx_survey_questions_id_survey", "public.idx_survey_question_id_survey"),
        ("public.idx_history_survey_questions_id_survey", "public.idx_history_survey_question_id_survey"),
        ("public.idx_history_answer_id_survey", "public.idx_answer_id_survey"),
        ("public.idx_history_answer_completion_date", "public.idx_answer_completion_date"),
        ("public.idx_history_answer_items_id_answer", "public.idx_answer_item_id_answer"),
        ("public.idx_history_answer_item_id_answer", "public.idx_answer_item_id_answer"),
        ("public.idx_access_extensions_lookup", "public.idx_access_extension_lookup"),
        ("public.idx_logs_date", "public.idx_log_date"),
        ("public.idx_logs_event_type", "public.idx_log_event_type"),
        ("public.idx_omsu_l_changed_at", "public.idx_organization_l_changed_at"),
        ("public.idx_organizations_l_changed_at", "public.idx_organization_l_changed_at"),
        ("public.idx_omsu_surveys_l_changed_at", "public.idx_organization_survey_l_changed_at"),
        ("public.idx_organization_surveys_l_changed_at", "public.idx_organization_survey_l_changed_at"),
        ("public.idx_users_l_changed_at", "public.idx_app_user_l_changed_at"),
        ("public.idx_surveys_l_changed_at", "public.idx_survey_l_changed_at"),
        ("public.idx_omsu_l_record_pk", "public.idx_organization_l_record_pk"),
        ("public.idx_organizations_l_record_pk", "public.idx_organization_l_record_pk"),
        ("public.idx_omsu_surveys_l_record_pk", "public.idx_organization_survey_l_record_pk"),
        ("public.idx_organization_surveys_l_record_pk", "public.idx_organization_survey_l_record_pk"),
        ("public.idx_users_l_record_pk", "public.idx_app_user_l_record_pk"),
        ("public.idx_surveys_l_record_pk", "public.idx_survey_l_record_pk"),
        ("public.idx_history_answer_l_changed_at", "public.idx_answer_l_changed_at"),
        ("public.idx_history_answer_l_record_pk", "public.idx_answer_l_record_pk")
    };

    private static readonly (string OldName, string NewName)[] DuplicateLegacyIndexes =
    {
        ("public.idx_organizations_l_changed_at", "public.idx_organization_l_changed_at"),
        ("public.idx_organizations_l_record_pk", "public.idx_organization_l_record_pk"),
        ("public.idx_organization_surveys_l_changed_at", "public.idx_organization_survey_l_changed_at"),
        ("public.idx_organization_surveys_l_record_pk", "public.idx_organization_survey_l_record_pk")
    };

    private static readonly (string OldName, string NewName)[] SequenceRenames =
    {
        ("public.omsu_id_omsu_seq", "public.organization_organization_id_seq"),
        ("public.organizations_organization_id_seq", "public.organization_organization_id_seq"),
        ("public.omsu_surveys_l_id_audit_seq", "public.organization_survey_l_id_audit_seq"),
        ("public.organization_surveys_l_id_audit_seq", "public.organization_survey_l_id_audit_seq"),
        ("public.users_id_user_seq", "public.app_user_id_user_seq"),
        ("public.users_l_id_audit_seq", "public.app_user_l_id_audit_seq"),
        ("public.surveys_id_survey_seq", "public.survey_id_survey_seq"),
        ("public.survey_questions_id_question_seq", "public.survey_question_id_question_seq"),
        ("public.history_surveys_id_hsurvey_seq", "public.history_survey_id_hsurvey_seq"),
        ("public.history_survey_questions_id_history_question_seq", "public.history_survey_question_id_history_question_seq"),
        ("public.history_answer_id_answer_seq", "public.answer_id_answer_seq"),
        ("public.history_answer_items_id_item_seq", "public.answer_item_id_item_seq"),
        ("public.history_answer_item_id_item_seq", "public.answer_item_id_item_seq"),
        ("public.access_extensions_id_seq", "public.access_extension_id_seq"),
        ("public.logs_id_log_seq", "public.log_id_log_seq"),
        ("public.omsu_l_id_audit_seq", "public.organization_l_id_audit_seq"),
        ("public.organizations_l_id_audit_seq", "public.organization_l_id_audit_seq"),
        ("public.surveys_l_id_audit_seq", "public.survey_l_id_audit_seq"),
        ("public.history_answer_l_id_audit_seq", "public.answer_l_id_audit_seq")
    };

    private static readonly object SyncRoot = new();
    private static bool _initialized;

    public static void EnsureInitialized(NpgsqlConnection connection)
    {
        if (_initialized)
        {
            return;
        }

        lock (SyncRoot)
        {
            if (_initialized)
            {
                return;
            }

            DropLegacyLogViewIfNeeded(connection);

            foreach (var rename in RelationRenames)
            {
                RenameRelationIfNeeded(connection, rename.OldName, rename.NewName);
            }

            foreach (var rename in ConstraintRenames)
            {
                RenameConstraintIfNeeded(connection, rename.TableName, rename.OldName, rename.NewName);
            }

            foreach (var rename in IndexRenames)
            {
                RenameIndexIfNeeded(connection, rename.OldName, rename.NewName);
            }

            foreach (var duplicate in DuplicateLegacyIndexes)
            {
                DropLegacyIndexIfDuplicate(connection, duplicate.OldName, duplicate.NewName);
            }

            foreach (var rename in SequenceRenames)
            {
                RenameSequenceIfNeeded(connection, rename.OldName, rename.NewName);
            }

            _initialized = true;
        }
    }

    private static void DropLegacyLogViewIfNeeded(NpgsqlConnection connection)
    {
        using var command = new NpgsqlCommand(
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM pg_class
                    WHERE relnamespace = 'public'::regnamespace
                      AND relkind = 'v'
                      AND relname = 'log'
                ) AND to_regclass('public.logs') IS NOT NULL THEN
                    EXECUTE 'DROP VIEW public.log';
                END IF;
            END $$;
            """,
            connection);

        command.ExecuteNonQuery();
    }

    private static void RenameRelationIfNeeded(NpgsqlConnection connection, string oldName, string newName)
    {
        if (!RelationExists(connection, oldName) || RelationExists(connection, newName))
        {
            return;
        }

        var (schemaName, oldRelationName) = SplitQualifiedName(oldName);
        var (_, newRelationName) = SplitQualifiedName(newName);

        ExecuteNonQuery(
            connection,
            $"ALTER TABLE {QuoteQualifiedName(schemaName, oldRelationName)} RENAME TO {QuoteIdentifier(newRelationName)};");
    }

    private static void RenameConstraintIfNeeded(NpgsqlConnection connection, string tableName, string oldName, string newName)
    {
        if (!RelationExists(connection, tableName)
            || !ConstraintExists(connection, tableName, oldName)
            || ConstraintExists(connection, tableName, newName))
        {
            return;
        }

        var (schemaName, relationName) = SplitQualifiedName(tableName);

        ExecuteNonQuery(
            connection,
            $"ALTER TABLE {QuoteQualifiedName(schemaName, relationName)} RENAME CONSTRAINT {QuoteIdentifier(oldName)} TO {QuoteIdentifier(newName)};");
    }

    private static void RenameIndexIfNeeded(NpgsqlConnection connection, string oldName, string newName)
    {
        if (!RelationExists(connection, oldName) || RelationExists(connection, newName))
        {
            return;
        }

        var (schemaName, oldIndexName) = SplitQualifiedName(oldName);
        var (_, newIndexName) = SplitQualifiedName(newName);

        ExecuteNonQuery(
            connection,
            $"ALTER INDEX {QuoteQualifiedName(schemaName, oldIndexName)} RENAME TO {QuoteIdentifier(newIndexName)};");
    }

    private static void RenameSequenceIfNeeded(NpgsqlConnection connection, string oldName, string newName)
    {
        if (!RelationExists(connection, oldName) || RelationExists(connection, newName))
        {
            return;
        }

        var (schemaName, oldSequenceName) = SplitQualifiedName(oldName);
        var (_, newSequenceName) = SplitQualifiedName(newName);

        ExecuteNonQuery(
            connection,
            $"ALTER SEQUENCE {QuoteQualifiedName(schemaName, oldSequenceName)} RENAME TO {QuoteIdentifier(newSequenceName)};");
    }

    private static void DropLegacyIndexIfDuplicate(NpgsqlConnection connection, string oldName, string newName)
    {
        if (!RelationExists(connection, oldName) || !RelationExists(connection, newName))
        {
            return;
        }

        var (schemaName, indexName) = SplitQualifiedName(oldName);
        ExecuteNonQuery(connection, $"DROP INDEX {QuoteQualifiedName(schemaName, indexName)};");
    }

    private static bool RelationExists(NpgsqlConnection connection, string relationName)
    {
        using var command = new NpgsqlCommand("SELECT to_regclass(@relationName) IS NOT NULL;", connection);
        command.Parameters.AddWithValue("@relationName", relationName);
        return (bool)(command.ExecuteScalar() ?? false);
    }

    private static bool ConstraintExists(NpgsqlConnection connection, string tableName, string constraintName)
    {
        using var command = new NpgsqlCommand(
            """
            SELECT EXISTS (
                SELECT 1
                FROM pg_constraint
                WHERE conrelid = @tableName::regclass
                  AND conname = @constraintName
            );
            """,
            connection);

        command.Parameters.AddWithValue("@tableName", tableName);
        command.Parameters.AddWithValue("@constraintName", constraintName);
        return (bool)(command.ExecuteScalar() ?? false);
    }

    private static void ExecuteNonQuery(NpgsqlConnection connection, string sql)
    {
        using var command = new NpgsqlCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    private static (string SchemaName, string ObjectName) SplitQualifiedName(string qualifiedName)
    {
        var parts = qualifiedName.Split('.', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            throw new ArgumentException($"Expected a schema-qualified name, got '{qualifiedName}'.", nameof(qualifiedName));
        }

        return (parts[0], parts[1]);
    }

    private static string QuoteQualifiedName(string schemaName, string objectName)
    {
        return $"{QuoteIdentifier(schemaName)}.{QuoteIdentifier(objectName)}";
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"")}\"";
    }
}
