CREATE TABLE IF NOT EXISTS pdm_object (
    id uuid PRIMARY KEY,
    object_type varchar(20) NOT NULL CHECK (object_type IN ('Assembly', 'Part', 'StandardPart')),
    designation varchar(100),
    name varchar(500) NOT NULL,
    current_version_id uuid NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_pdm_object_designation
    ON pdm_object(designation)
    WHERE designation IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_pdm_object_standard_name
    ON pdm_object(name)
    WHERE object_type = 'StandardPart';

CREATE TABLE IF NOT EXISTS object_version (
    id uuid PRIMARY KEY,
    object_id uuid NOT NULL REFERENCES pdm_object(id) ON DELETE CASCADE,
    version_no integer NOT NULL CHECK (version_no > 0),
    state varchar(20) NOT NULL CHECK (state IN ('InWork', 'Approved', 'Cancelled')),
    material varchar(500),
    mass_kg numeric(18, 6) CHECK (mass_kg IS NULL OR mass_kg >= 0),
    created_at timestamp NOT NULL,
    CONSTRAINT ux_object_version_number UNIQUE (object_id, version_no)
);

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_pdm_object_current_version'
    ) THEN
        ALTER TABLE pdm_object
            ADD CONSTRAINT fk_pdm_object_current_version
            FOREIGN KEY (current_version_id) REFERENCES object_version(id)
            DEFERRABLE INITIALLY DEFERRED;
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS bom_link (
    id uuid PRIMARY KEY,
    parent_version_id uuid NOT NULL REFERENCES object_version(id) ON DELETE CASCADE,
    child_object_id uuid NOT NULL REFERENCES pdm_object(id) ON DELETE RESTRICT,
    quantity integer NOT NULL CHECK (quantity > 0),
    CONSTRAINT ux_bom_parent_child UNIQUE (parent_version_id, child_object_id)
);

CREATE TABLE IF NOT EXISTS import_log (
    id uuid PRIMARY KEY,
    started_at timestamp NOT NULL,
    file_name varchar(500) NOT NULL,
    severity varchar(20) NOT NULL CHECK (severity IN ('Accepted', 'Warning', 'Error')),
    reason text NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_object_version_object_id ON object_version(object_id);
CREATE INDEX IF NOT EXISTS ix_bom_parent_version ON bom_link(parent_version_id);
CREATE INDEX IF NOT EXISTS ix_bom_child_object ON bom_link(child_object_id);
