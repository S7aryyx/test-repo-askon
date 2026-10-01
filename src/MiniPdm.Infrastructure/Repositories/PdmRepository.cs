using Dapper;
using MiniPdm.Application.Interfaces;
using MiniPdm.Domain.Entities;
using MiniPdm.Domain.Enums;
using MiniPdm.Domain.Models;

namespace MiniPdm.Infrastructure.Repositories;

public sealed class PdmRepository : IPdmRepository
{
    private readonly Database.DbSession _session;

    public PdmRepository(Database.DbSession session) => _session = session;

    private CommandDefinition Command(string sql, object? parameters = null, CancellationToken ct = default)
        => new(sql, parameters, _session.Transaction, cancellationToken: ct);

    public async Task<PdmObject?> GetByIdentityAsync(ObjectType type, string? designation, string name, CancellationToken ct)
    {
        const string sql = """
            SELECT id, object_type AS ObjectType, designation, name, current_version_id AS CurrentVersionId
            FROM pdm_object
            WHERE object_type = @Type
              AND ((@Designation IS NOT NULL AND designation = @Designation)
                OR (@Designation IS NULL AND name = @Name));
            """;
        return await _session.Connection.QuerySingleOrDefaultAsync<PdmObject>(Command(sql, new { Type = type.ToString(), Designation = designation, Name = name }, ct));
    }

    public async Task<PdmObject?> GetByIdAsync(Guid id, CancellationToken ct)
        => await _session.Connection.QuerySingleOrDefaultAsync<PdmObject>(Command("SELECT id, object_type AS ObjectType, designation, name, current_version_id AS CurrentVersionId FROM pdm_object WHERE id=@Id;", new { id }, ct));

    public async Task<ObjectVersion?> GetCurrentVersionAsync(Guid objectId, CancellationToken ct)
    {
        const string sql = """
            SELECT v.id, v.object_id AS ObjectId, v.version_no AS VersionNo, v.state AS State,
                   v.material, v.mass_kg AS MassKg, v.created_at AS CreatedAt
            FROM object_version v JOIN pdm_object o ON o.current_version_id=v.id
            WHERE o.id=@ObjectId;
            """;
        return await _session.Connection.QuerySingleOrDefaultAsync<ObjectVersion>(Command(sql, new { objectId }, ct));
    }

    public async Task<ObjectVersion?> GetVersionAsync(Guid versionId, CancellationToken ct)
    {
        const string sql = """
            SELECT id, object_id AS ObjectId, version_no AS VersionNo, state AS State,
                   material, mass_kg AS MassKg, created_at AS CreatedAt
            FROM object_version WHERE id=@Id;
            """;
        return await _session.Connection.QuerySingleOrDefaultAsync<ObjectVersion>(Command(sql, new { Id = versionId }, ct));
    }

    public async Task<ObjectVersion?> GetLatestNonCancelledVersionAsync(Guid objectId, CancellationToken ct)
    {
        const string sql = """
            SELECT id, object_id AS ObjectId, version_no AS VersionNo, state AS State,
                   material, mass_kg AS MassKg, created_at AS CreatedAt
            FROM object_version
            WHERE object_id=@ObjectId AND state <> 'Cancelled'
            ORDER BY version_no DESC LIMIT 1;
            """;
        return await _session.Connection.QuerySingleOrDefaultAsync<ObjectVersion>(Command(sql, new { objectId }, ct));
    }

    public async Task<IReadOnlyList<PdmObject>> SearchAsync(string text, CancellationToken ct)
    {
        const string sql = """
            SELECT id, object_type AS ObjectType, designation, name, current_version_id AS CurrentVersionId
            FROM pdm_object
            WHERE COALESCE(designation,'') ILIKE '%' || @Text || '%' OR name ILIKE '%' || @Text || '%'
            ORDER BY name;
            """;
        return (await _session.Connection.QueryAsync<PdmObject>(Command(sql, new { text }, ct))).ToList();
    }

    public async Task<IReadOnlyList<PdmObject>> GetAssembliesAsync(CancellationToken ct)
    {
        const string sql = "SELECT id, object_type AS ObjectType, designation, name, current_version_id AS CurrentVersionId FROM pdm_object WHERE object_type='Assembly' ORDER BY name;";
        return (await _session.Connection.QueryAsync<PdmObject>(Command(sql, null, ct))).ToList();
    }

    public async Task<IReadOnlyList<BomLink>> GetBomLinksAsync(Guid parentVersionId, CancellationToken ct)
    {
        const string sql = """
            SELECT id, parent_version_id AS ParentVersionId, child_object_id AS ChildObjectId, quantity
            FROM bom_link WHERE parent_version_id=@ParentVersionId ORDER BY id;
            """;
        var result = await _session.Connection.QueryAsync<BomLink>(Command(sql, new { ParentVersionId = parentVersionId }, ct));
        return result.ToList();
    }

    public async Task<IReadOnlyList<PdmObject>> GetChildrenAsync(Guid parentVersionId, CancellationToken ct)
    {
        const string sql = """
            SELECT o.id, o.object_type AS ObjectType, o.designation, o.name, o.current_version_id AS CurrentVersionId
            FROM bom_link b JOIN pdm_object o ON o.id=b.child_object_id
            WHERE b.parent_version_id=@parentVersionId ORDER BY o.name;
            """;
        return (await _session.Connection.QueryAsync<PdmObject>(Command(sql, new { parentVersionId }, ct))).ToList();
    }

    public async Task<Guid> InsertObjectAsync(PdmObject item, CancellationToken ct)
    {
        const string sql = "INSERT INTO pdm_object(id, object_type, designation, name, current_version_id) VALUES(@Id,@ObjectType,@Designation,@Name,@CurrentVersionId);";
        await _session.Connection.ExecuteAsync(Command(sql, new { item.Id, ObjectType=item.ObjectType.ToString(), item.Designation, item.Name, item.CurrentVersionId }, ct));
        return item.Id;
    }

    public async Task<Guid> InsertVersionAsync(ObjectVersion version, CancellationToken ct)
    {
        const string sql = "INSERT INTO object_version(id,object_id,version_no,state,material,mass_kg,created_at) VALUES(@Id,@ObjectId,@VersionNo,@State,@Material,@MassKg,@CreatedAt);";
        await _session.Connection.ExecuteAsync(Command(sql, new { version.Id, version.ObjectId, version.VersionNo, State=version.State.ToString(), version.Material, version.MassKg, version.CreatedAt }, ct));
        return version.Id;
    }

    public async Task UpdateObjectAsync(PdmObject item, CancellationToken ct)
    {
        const string sql = "UPDATE pdm_object SET name=@Name, designation=@Designation WHERE id=@Id;";
        await _session.Connection.ExecuteAsync(Command(sql, new { item.Id, item.Name, item.Designation }, ct));
    }

    public async Task UpdateVersionAsync(ObjectVersion version, CancellationToken ct)
    {
        const string sql = "UPDATE object_version SET material=@Material,mass_kg=@MassKg WHERE id=@Id;";
        await _session.Connection.ExecuteAsync(Command(sql, new { version.Id, version.Material, version.MassKg }, ct));
    }

    public async Task UpdateStateAsync(Guid versionId, ObjectState state, CancellationToken ct)
    {
        const string sql = "UPDATE object_version SET state=@State WHERE id=@Id;";
        await _session.Connection.ExecuteAsync(Command(sql, new { Id = versionId, State = state.ToString() }, ct));
    }

    public async Task ReplaceBomAsync(Guid parentVersionId, IReadOnlyCollection<BomLink> links, CancellationToken ct)
    {
        await _session.Connection.ExecuteAsync(Command("DELETE FROM bom_link WHERE parent_version_id=@parentVersionId;", new { parentVersionId }, ct));
        const string sql = "INSERT INTO bom_link(id,parent_version_id,child_object_id,quantity) VALUES(@Id,@ParentVersionId,@ChildObjectId,@Quantity);";
        foreach (var link in links)
            await _session.Connection.ExecuteAsync(Command(sql, link, ct));
    }

    public async Task SetCurrentVersionAsync(Guid objectId, Guid? versionId, CancellationToken ct)
        => await _session.Connection.ExecuteAsync(Command("UPDATE pdm_object SET current_version_id=@versionId WHERE id=@objectId;", new { objectId, versionId }, ct));

    public async Task<int> GetNextVersionNumberAsync(Guid objectId, CancellationToken ct)
        => await _session.Connection.ExecuteScalarAsync<int>(Command("SELECT COALESCE(MAX(version_no),0)+1 FROM object_version WHERE object_id=@objectId;", new { objectId }, ct));

    public async Task<IReadOnlyList<BomNode>> GetBomTreeAsync(Guid assemblyObjectId, CancellationToken ct)
    {
        const string sql = """
            WITH RECURSIVE bom AS (
                SELECT o.id object_id, v.id version_id, NULL::uuid parent_object_id,
                       o.name, o.designation, o.object_type, 1::int quantity, 0 level,
                       ARRAY[o.id] path
                FROM pdm_object o JOIN object_version v ON v.id=o.current_version_id
                WHERE o.id=@assemblyObjectId
                UNION ALL
                SELECT child.id, child.current_version_id, bom.object_id,
                       child.name, child.designation, child.object_type,
                       bom.quantity * link.quantity, bom.level + 1,
                       bom.path || child.id
                FROM bom
                JOIN bom_link link ON link.parent_version_id=bom.version_id
                JOIN pdm_object child ON child.id=link.child_object_id
                WHERE NOT child.id = ANY(bom.path)
            )
            SELECT object_id ObjectId, version_id VersionId, parent_object_id ParentObjectId,
                   name, designation, object_type ObjectType, quantity, level, path AS Path
            FROM bom ORDER BY level, name;
            """;
        return (await _session.Connection.QueryAsync<BomNode>(Command(sql, new { assemblyObjectId }, ct))).ToList();
    }

    public async Task<IReadOnlyList<SpecificationRow>> GetSpecificationAsync(Guid assemblyObjectId, CancellationToken ct)
    {
        const string sql = """
            WITH RECURSIVE bom AS (
                SELECT o.id object_id, v.id version_id, 1::bigint quantity, ARRAY[o.id] path
                FROM pdm_object o JOIN object_version v ON v.id=o.current_version_id
                WHERE o.id=@assemblyObjectId
                UNION ALL
                SELECT child.id, child.current_version_id,
                       bom.quantity * link.quantity, bom.path || child.id
                FROM bom
                JOIN bom_link link ON link.parent_version_id=bom.version_id
                JOIN pdm_object child ON child.id=link.child_object_id
                WHERE NOT child.id = ANY(bom.path)
            )
            SELECT b.object_id ObjectId, o.name, o.designation, o.object_type ObjectType,
                   SUM(b.quantity)::int Quantity, v.mass_kg MassKg
            FROM bom b
            JOIN pdm_object o ON o.id=b.object_id
            JOIN object_version v ON v.id=o.current_version_id
            WHERE o.object_type <> 'Assembly'
            GROUP BY b.object_id,o.name,o.designation,o.object_type,v.mass_kg
            ORDER BY o.name;
            """;
        return (await _session.Connection.QueryAsync<SpecificationRow>(Command(sql, new { assemblyObjectId }, ct))).ToList();
    }
}
