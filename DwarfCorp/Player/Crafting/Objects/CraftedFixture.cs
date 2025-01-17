﻿using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using DwarfCorp.GameStates;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using System.Linq;

namespace DwarfCorp
{
    public class CraftedFixture : Fixture
    {
        public CraftedFixture()
        {
            this.SetFlag(Flag.ShouldSerialize, true);
        }

        public CraftedFixture(ComponentManager manager, Vector3 position, SpriteSheet sheet, Point frame, CraftDetails details, SimpleSprite.OrientMode OrientMode = SimpleSprite.OrientMode.Spherical) :
            base(manager, position, sheet, frame, OrientMode)
        {
            this.SetFlag(Flag.ShouldSerialize, true);
            details.DebugColor = Color.Brown;
            AddChild(details);
        }

        public CraftedFixture(
            String Name,
            IEnumerable<String> Tags,
            ComponentManager Manager,
            Vector3 Position,
            SpriteSheet Sheet,
            Point Sprite,
            Resource Resource)
            : base(Name, Tags, Manager, Position, Sheet, Sprite)
        {
            this.SetFlag(Flag.ShouldSerialize, true);
            AddChild(new CraftDetails(Manager, Resource) { DebugColor = Color.Brown });
        }
    }
}
