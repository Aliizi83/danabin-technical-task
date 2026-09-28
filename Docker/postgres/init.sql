-- Runs once, on the first start of an empty postgres volume.
GRANT ALL PRIVILEGES ON DATABASE danatadbir TO core_user;

GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO core_user;
GRANT ALL PRIVILEGES ON ALL FUNCTIONS IN SCHEMA public TO core_user;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO core_user;
