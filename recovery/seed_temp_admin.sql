BEGIN;

-- Temporary recovery account.
-- Login: admin
-- Password: TempAdmin12345!
--
-- The hash below is a legacy SHA-512 Base64 value because AuthController
-- supports legacy hashes and automatically rehashes them on the next login.
-- Change the password immediately after the first login.

INSERT INTO public.users (
    id_omsu,
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
    'ztEGidTzYesYLtKhQY4so3/en72zKn4VeKzIe3ujbNLcLqmftEs1nLulipGHiwTE8RhjXNCjEd+PFpYLK/uk0Q==',
    'local-admin@example.invalid',
    NOW()
)
ON CONFLICT (name_user) DO NOTHING;

COMMIT;
