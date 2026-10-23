"""Connection parsing checks use synthetic values and never contact a database."""
import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("dev_db", Path(__file__).resolve().parents[1] / "scripts/dev-db.py")
database = importlib.util.module_from_spec(spec)
spec.loader.exec_module(database)


class DevelopmentDatabaseConfigurationTests(unittest.TestCase):
    def test_quoted_password_can_contain_semicolons_and_doubled_quotes(self):
        result = database.connection_environment('Host=localhost;Database=test;Username=test;Password="synthetic;value""quoted"')
        self.assertEqual('synthetic;value"quoted', result["PGPASSWORD"])

    def test_url_percent_encoding_is_decoded(self):
        result = database.connection_environment("postgresql://user:synthetic%3Bvalue@localhost/test?sslmode=verify-full")
        self.assertEqual("synthetic;value", result["PGPASSWORD"])
        self.assertEqual("verify-full", result["PGSSLMODE"])

    def test_inherited_libpq_target_and_options_are_removed(self):
        with patch.dict("os.environ", {"PGSERVICE": "other-database", "PGOPTIONS": "-c search_path=other", "PGDATABASE": "other"}):
            result = database.connection_environment("Host=localhost;Database=test;Username=test")
        self.assertNotIn("PGSERVICE", result)
        self.assertNotIn("PGOPTIONS", result)
        self.assertEqual("test", result["PGDATABASE"])

    def test_unsupported_or_ambiguous_targets_are_rejected(self):
        for connection in [
            "Host=localhost;Database=test;Username=test;Unknown=1",
            "Host=localhost;Database=test;Username=test;Host=other",
            "Host=localhost,other;Database=test;Username=test",
            "Host=localhost;Username=test", "Host=localhost;Database=test;Username=test;Port=0",
        ]:
            with self.subTest(connection=connection), self.assertRaises(database.DatabaseError):
                database.connection_environment(connection)

    def test_password_and_dsn_are_redacted(self):
        self.assertEqual("[REDACTED] [REDACTED]", database.redact("dsn synthetic-secret", "dsn", {"PGPASSWORD": "synthetic-secret"}))

    def test_restore_without_confirmation_never_reads_connection(self):
        with patch.object(database, "read_connection") as read:
            self.assertEqual(1, database.main(["restore", "nonexistent.dump"]))
            read.assert_not_called()

    def test_backup_default_is_workspace_level_not_inside_repository(self):
        expected = database.ROOT.parent / "backups"
        self.assertEqual(expected.resolve(), database.configured_backup_directory(str(expected)))
        self.assertNotEqual(database.ROOT / "backups", database.configured_backup_directory(str(expected)))

    def test_backup_directory_environment_override_and_relative_path(self):
        with tempfile.TemporaryDirectory() as temporary:
            override = Path(temporary) / "custom-archives"
            with patch.dict("os.environ", {"AI_SMART_INVENTORY_BACKUP_DIR": str(override)}):
                self.assertEqual(override.resolve(), database.configured_backup_directory())
            relative = "safe-backup-test-folder"
            expected = (database.ROOT.parent / relative).resolve()
            with patch.dict("os.environ", {"AI_SMART_INVENTORY_BACKUP_DIR": relative}):
                actual = database.configured_backup_directory()
            self.assertEqual(expected, actual)
            actual.rmdir()

    def test_backup_output_must_be_a_single_filename(self):
        with tempfile.TemporaryDirectory() as temporary:
            folder = Path(temporary).resolve()
            self.assertEqual(folder / "safe.dump", database.backup_target_path(folder, Path("safe.dump"), "unused.dump"))
            for unsafe in (Path("../outside.dump"), Path("folder/file.dump"), Path("/tmp/outside.dump")):
                with self.subTest(unsafe=unsafe), self.assertRaises(database.DatabaseError):
                    database.backup_target_path(folder, unsafe, "unused.dump")


if __name__ == "__main__":
    unittest.main()
