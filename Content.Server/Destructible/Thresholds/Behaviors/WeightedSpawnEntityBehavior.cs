using System.Numerics;
using Content.Server.Spawners.Components;
using Content.Server.Spawners.EntitySystems;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Spawners;
using Robust.Shared.Random;

namespace Content.Server.Destructible.Thresholds.Behaviors;

/// <summary>
/// Behavior that can be assigned to a trigger that that takes a <see cref="WeightedRandomEntityPrototype"/>
/// and spawns a number of the same entity between a given min and max
/// at a random offset from the final position of the entity.
/// </summary>
[Serializable]
[DataDefinition]
public sealed partial class WeightedSpawnEntityBehavior : IThresholdBehavior
{
    private static readonly EntProtoId TempEntityProtoId = "TemporaryEntityForTimedDespawnSpawners";

    /// <summary>
    /// A table of entities with assigned weights to randomly pick from
    /// </summary>
    [DataField(required: true)]
    public ProtoId<WeightedRandomEntityPrototype> WeightedEntityTable;

    /// <summary>
    /// How far away to spawn the entity from the parent position
    /// </summary>
    [DataField]
    public float SpawnOffset = 1;

    /// <summary>
    /// The mininum number of entities to spawn randomly
    /// </summary>
    [DataField]
    public int MinSpawn = 1;

    /// <summary>
    /// The max number of entities to spawn randomly
    /// </summary>
    [DataField]
    public int MaxSpawn = 1;

    /// <summary>
    /// Time in seconds to wait before spawning entities
    /// </summary>
    [DataField]
    public float SpawnAfter;

    /// <summary>
    /// Should entities be attached to the tiles?
    /// </summary>
    [DataField]
    public bool SpawnAtTiles = false;

    /// <summary>
    /// Should multiple entities spawn at the same tile?
    /// </summary>
    [DataField]
    public bool SpawnIntersecting = false;

    public void Execute(EntityUid uid, DestructibleSystem system, EntityUid? cause = null)
    {
        // Get the position at which to start initially spawning entities
        var transform = system.EntityManager.System<TransformSystem>();
        var position = transform.GetMapCoordinates(uid);
        List<Vector2> usedCoords = [];
        // Helper function used to randomly get an offset to apply to the original position
        Vector2 GetRandomCoordinates(bool tile, bool intersecting, List<Vector2>? usedCoords = null)
        {
            Vector2 coords = new(system.Random.NextFloat(-SpawnOffset, SpawnOffset), system.Random.NextFloat(-SpawnOffset, SpawnOffset));

            if (tile)
                coords.Rounded();

            if (intersecting)
            {
                do
                {
                    coords = new(system.Random.NextFloat(-SpawnOffset, SpawnOffset), system.Random.NextFloat(-SpawnOffset, SpawnOffset));
                    if (tile)
                        coords = coords.Rounded();
                }
                while (system.EntityManager.System<EntityLookupSystem>().AnyEntitiesIntersecting(position.Offset(coords), LookupFlags.Static));
            }

            if (usedCoords != null)
            {
                do
                {
                    coords = new(system.Random.NextFloat(-SpawnOffset, SpawnOffset), system.Random.NextFloat(-SpawnOffset, SpawnOffset));
                    if (tile)
                        coords = coords.Rounded();
                }
                while (usedCoords.Contains(coords));
            }

            return new(system.Random.NextFloat(-SpawnOffset, SpawnOffset), system.Random.NextFloat(-SpawnOffset, SpawnOffset));
        }
        // Randomly pick the entity to spawn and randomly pick how many to spawn
        var entity = system.PrototypeManager.Index(WeightedEntityTable).Pick(system.Random);
        var amountToSpawn = system.Random.NextFloat(MinSpawn, MaxSpawn);

        // Different behaviors for delayed spawning and immediate spawning
        if (SpawnAfter != 0)
        {
            // if it fails to get the spawner, this won't ever work so just return
            if (!system.PrototypeManager.Resolve(TempEntityProtoId, out var tempSpawnerProto))
                return;

            // spawn the spawner, assign it a lifetime, and assign the entity that it will spawn when despawned
            for (var i = 0; i < amountToSpawn; i++)
            {
                Vector2 target;
                if (SpawnIntersecting)
                {
                    target = GetRandomCoordinates(SpawnAtTiles, SpawnIntersecting, usedCoords);
                    usedCoords.Add(target);
                }
                else
                    target = GetRandomCoordinates(SpawnAtTiles, SpawnIntersecting);

                var spawner = system.EntityManager.SpawnEntity(tempSpawnerProto.ID, position.Offset(target));
                system.EntityManager.EnsureComponent<TimedDespawnComponent>(spawner, out var timedDespawnComponent);
                timedDespawnComponent.Lifetime = SpawnAfter;
                system.EntityManager.EnsureComponent<SpawnOnDespawnComponent>(spawner, out var spawnOnDespawnComponent);
                system.EntityManager.System<SpawnOnDespawnSystem>().SetPrototype((spawner, spawnOnDespawnComponent), entity);
            }
        }
        else
        {
            // directly spawn the desired entities
            for (var i = 0; i < amountToSpawn; i++)
            {
                Vector2 target;
                if (SpawnIntersecting)
                {
                    target = GetRandomCoordinates(SpawnAtTiles, SpawnIntersecting, usedCoords);
                    usedCoords.Add(target);
                }
                else
                    target = GetRandomCoordinates(SpawnAtTiles, SpawnIntersecting);

                system.EntityManager.SpawnEntity(entity, position.Offset(target));
            }
        }
    }
}
