using System;
using System.Collections.Generic;
using System.Linq;
using DwarfCorp.GameStates;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Reflection;

namespace DwarfCorp
{
    public static class DebugFactories
    {
        [EntityFactory("RandTrinket")]
        private static GameComponent __factory0(ComponentManager Manager, Vector3 Position, Blackboard Data)
        {
            if (Library.CreateTrinketResourceType(Datastructures.SelectRandom(Library.EnumerateResourceTypes().Where(r => r.Tags.Contains("Material"))).Name, MathFunctions.Rand(0.1f, 3.5f)).HasValue(out var randResource))
            {

                if (MathFunctions.RandEvent(0.5f))
                    if (Library.CreateEncrustedTrinketResourceType(randResource.Name, Datastructures.SelectRandom(Library.EnumerateResourceTypes().Where(r => r.Tags.Contains("Gem"))).Name).HasValue(out var _rr))
                        randResource = _rr;

                return new ResourceEntity(Manager, new ResourceAmount(randResource.Name, 1), Position);
            }

            return null;
        }

        [EntityFactory("RandFood")]
        private static GameComponent __factory1(ComponentManager Manager, Vector3 Position, Blackboard Data)
        {
            var foods = Library.EnumerateResourceTypesWithTag("RawFood");
            if (Library.CreateMealResourceType(Datastructures.SelectRandom(foods).Name, Datastructures.SelectRandom(foods).Name).HasValue(out var randResource))
                return new ResourceEntity(Manager, new ResourceAmount(randResource.Name, 1), Position);
            return null;
        }
    }
}
