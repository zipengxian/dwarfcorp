using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using DwarfCorp.GameStates;
using Microsoft.Xna.Framework;

namespace DwarfCorp
{
    public class TurretTrap : CraftedBody
    {
        [EntityFactory("Turret")]
        private static GameComponent __factory(ComponentManager Manager, Vector3 Position, Blackboard Data)
        {
            return new TurretTrap(Manager, Position, Manager.World.PlayerFaction, Data.GetData<List<ResourceAmount>>("Resources", null));
        }

        public Attack Weapon { get; set; }
        public SimpleSprite BaseSprite { get; set; }
        public SimpleSprite TurretSprite { get; set; }
        public Faction Allies { get; set; }
        public EnemySensor Sensor { get; set; }
        private CreatureAI closestCreature = null;
        private Vector3 offset = Vector3.Zero;
        private float _currentAngle = 0.0f;
        private float _targetAngle = 0.0f;
        [OnDeserialized]
        public void OnDeserialized(StreamingContext ctx)
        {
            Sensor.OnEnemySensed += Sensor_OnEnemySensed;
        }

        public TurretTrap()
        {
            
        }

        public TurretTrap(ComponentManager manager, Vector3 position, Faction faction, List<ResourceAmount> resources) :
            base(manager, "TurretTrap", Matrix.CreateTranslation(position),
            new Vector3(1.0f, 1.0f, 1.0f), Vector3.Zero, new CraftDetails(manager, "Turret", resources))
        {
            Allies = faction;
            SpriteSheet spriteSheet = new SpriteSheet(ContentPaths.Entities.Furniture.interior_furniture, 32, 32);
            Weapon = new Attack("BowAttack", 5.0f, 1.0f, 5.0f, SoundSource.Create(ContentPaths.Audio.Oscar.sfx_trap_turret_shoot_1, ContentPaths.Audio.Oscar.sfx_trap_turret_shoot_2), ContentPaths.Effects.pierce)
            {
                ProjectileType = "Arrow",
                Mode = Attack.AttackMode.Ranged,
                LaunchSpeed = 15
            };

            AddChild(new ParticleTrigger("explode", Manager, "DeathParticles",
            Matrix.Identity, new Vector3(0.5f, 0.5f, 0.5f), Vector3.Zero)
            {
                SoundToPlay = ContentPaths.Audio.Oscar.sfx_trap_turret_shoot_1
            });

            AddChild(new Health(Manager, "health", 50.0f, 0.0f, 50.0f));

            Sensor = AddChild(new EnemySensor(Manager, "sensor", Matrix.Identity, new Vector3(8, 8, 8),
                Vector3.Zero)
            {
                Allies = faction
            }) as EnemySensor;

            Sensor.OnEnemySensed += Sensor_OnEnemySensed;
            BaseSprite = AddChild(new SimpleSprite(Manager, "Turret", Matrix.Identity, spriteSheet, new Point(2, 7))) as SimpleSprite;
            BaseSprite.OrientationType = SimpleSprite.OrientMode.YAxis;
            TurretSprite = AddChild(new SimpleSprite(Manager, "Turret", Matrix.CreateTranslation(Vector3.Up * 0.25f), spriteSheet, new Point(1, 7))) as SimpleSprite;
            TurretSprite.OrientationType = SimpleSprite.OrientMode.Fixed;
            SetTurretAngle(0.0f);
            CreateCosmeticChildren(manager);
            AddChild(new MagicalObject(Manager));
        }

        public override void CreateCosmeticChildren(ComponentManager manager)
        {
            AddChild(new Shadow(manager));

            AddChild(new GenericVoxelListener(Manager, Matrix.Identity, new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0.0f, -1.0f, 0.0f), (changeEvent) =>
            {
                if (changeEvent.Type == VoxelChangeEventType.VoxelTypeChanged && changeEvent.NewVoxelType == 0)
                    Die();
            })).SetFlag(Flag.ShouldSerialize, false);
            base.CreateCosmeticChildren(manager);
        }

        void Sensor_OnEnemySensed(List<CreatureAI> enemies)
        {
            if (!Active)
                return;

            closestCreature = null;
            float minDist = float.MaxValue;
            foreach (CreatureAI enemy in enemies)
            {
                float dist = (enemy.Position - Position).LengthSquared();

                if (dist < minDist)
                {
                    offset = enemy.Position - Position;
                    minDist = dist;
                    closestCreature = enemy;
                }
            }
        }

        public void SetTurretAngle(float radians)
        {
            Matrix turretTransform = Matrix.CreateRotationX(1.57f) * Matrix.CreateRotationY(radians + 1.57f);
            turretTransform.Translation = Vector3.Up * 0.25f;
            TurretSprite.LocalTransform = turretTransform;
            PropogateTransforms();
        }

        override public void Update(DwarfTime gameTime, ChunkManager chunks, Camera camera)
        {
            base.Update(gameTime, chunks, camera);

            if (Active && closestCreature != null && !closestCreature.IsDead)
            {                
                Weapon.RechargeTimer.Update(gameTime);

                _targetAngle = (float)Math.Atan2(offset.X, offset.Z);

                if (Weapon.RechargeTimer.HasTriggered)
                {
                    closestCreature.Kill(this);
                    Weapon.LaunchProjectile(Position + Vector3.Up * 0.5f, closestCreature.Position, closestCreature.Physics);
                    Weapon.PlayNoise(Position);
                    Weapon.RechargeTimer.Reset();
                    GetComponent<MagicalObject>().CurrentCharges--;
                }
            }
            if (Math.Abs(_currentAngle - _targetAngle) > 0.001f)
            {
                _currentAngle = 0.9f * _currentAngle + 0.1f * _targetAngle;
                SetTurretAngle(_currentAngle);
            }
        }
    }
}
