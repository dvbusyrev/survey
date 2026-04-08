BEGIN;

-- Temporary recovery account.
-- Login: admin
-- Password: TempAdmin12345!
--
-- The hash below uses the current ASP.NET Identity password hasher.
-- Change the password immediately after the first login.

INSERT INTO public.app_user (
    organization_id,
    name_user,
    full_name,
    name_role,
    hash_password,
    email,
    date_begin
)
VALUES (
    NULL,
    'admin',
    'Временный администратор',
    'admin',
    'AQAAAAIAAYagAAAAECcXCw02F04Jo/TTU8ZmOPQfnOk33wQ4KQ+ZARJ+Y3uVStS73oC/HxtKvpWJ1VQOcA==',
    'local-admin@example.invalid',
    NOW()
)
ON CONFLICT (name_user) DO NOTHING;

COMMIT;
