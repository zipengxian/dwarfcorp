using System;
using System.Collections.Generic;
using System.Linq;
using DwarfCorp.GameStates;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using System.Runtime.Serialization;

namespace DwarfCorp
{
    public class BuildZoneOrder
    {
        public Zone ToBuild { get; set; }
        public ResourceSet PutResources;
        public List<BuildVoxelOrder> VoxelOrders { get; set; }
        public List<GameComponent> WorkObjects = new List<GameComponent>(); 
        public bool IsBuilt { get; set; }
        public float BuildProgress { get; set; }
        [JsonIgnore]
        private WorldManager World { get; set; }
        public bool IsDestroyed { get; set; }
        public CreatureAI ResourcesReservedFor = null;
        [JsonIgnore]
        public Gui.Widget DisplayWidget = null;

        [OnDeserialized]
        public void OnDeserialized(StreamingContext ctx)
        {
            World = (WorldManager)ctx.Context;
        }

        public BuildZoneOrder()
        {
            BuildProgress = 0;
        }


        public BuildZoneOrder(Zone toBuild, WorldManager world)
        {
            BuildProgress = 0;
            World = world;
            ToBuild = toBuild;
            PutResources = new ResourceSet();
            VoxelOrders = new List<BuildVoxelOrder>();
            IsBuilt = false;
            IsDestroyed = false;
        }
        
        public void AddResources(List<Resource> resources)
        {
            foreach (var res in resources)
                PutResources.Add(res);
        }

        public bool MeetsBuildRequirements()
        {
            bool toReturn = true;
            foreach (var s in ToBuild.Type.RequiredResources)
            {
                var required = Math.Max((int)(s.Value.Count * VoxelOrders.Count * 0.25f), 1);
                var has = PutResources.CountWithTag(s.Value.Tag);
                toReturn = toReturn && has >= required;
            }

            return toReturn;
        }

        public virtual void Build(bool silent=false)
        {
            if(IsBuilt)
                return;
            IsBuilt = true;

            ToBuild.CompleteRoomImmediately(VoxelOrders.Select(o => o.Voxel).ToList());

            if (!silent)
            {
                World.MakeAnnouncement(String.Format("{0} was built", ToBuild.Type.Name), null);
                SoundManager.PlaySound(ContentPaths.Audio.Oscar.sfx_gui_positive_generic, 0.15f);
            }

            foreach (GameComponent fence in WorkObjects)
                fence.Die();
        }

        public void Destroy()
        {
            ToBuild.Destroy();
            foreach (GameComponent fence in WorkObjects)
            {
                fence.Die();
            }
            IsDestroyed = true;
        }

        public void SetTint(Color color)
        {
            foreach (var fence in WorkObjects)
            {
                SetDisplayColor(fence, color);
            }
        }

        private void SetDisplayColor(GameComponent body, Color color)
        {
            foreach (var sprite in body.EnumerateAll().OfType<Tinter>().ToList())
                sprite.VertexColorTint = color;
        }

        public BoundingBox GetBoundingBox()
        {
            List<BoundingBox> components = VoxelOrders.Select(vox => vox.Voxel.GetBoundingBox()).ToList();

            return MathFunctions.GetBoundingBox(components);
        }

        public bool IsResourceSatisfied(String Tag)
        {
            int required = GetNumRequiredResources(Tag);
            int current = PutResources.CountWithTag(Tag);
            return current >= required;
        }

        public int GetNumRequiredResources(String name)
        {
            if(ToBuild.Type.RequiredResources.ContainsKey(name))
            {
                return Math.Max((int) (ToBuild.Type.RequiredResources[name].Count * VoxelOrders.Count * 0.25f), 1);
            }
            else
            {
                return 0;
            }
        }

        public string GetTextDisplay()
        {
            string toReturn = ToBuild.Type.Name;

            foreach (var amount in ToBuild.Type.RequiredResources.Values)
            {
                toReturn += "\n";
                int numResource = PutResources.CountWithTag(amount.Tag);
                toReturn += amount.Tag.ToString() + " : " + numResource + "/" + Math.Max((int) (amount.Count * VoxelOrders.Count * 0.25f), 1);
            }

            return toReturn;
        }

        public List<ResourceTagAmount> ListRequiredResources()
        {
            var toReturn = new List<ResourceTagAmount>();
            foreach (String s in ToBuild.Type.RequiredResources.Keys)
            {
                int needed = Math.Max((int) (ToBuild.Type.RequiredResources[s].Count * VoxelOrders.Count * 0.25f), 1);
                int currentResources = PutResources.CountWithTag(s);

                if(currentResources >= needed)
                {
                    continue;
                }

                toReturn.Add(new ResourceTagAmount(s, needed - currentResources));
            }

            return toReturn;
        }
    }

}
