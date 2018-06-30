// OrientedAnimation.cs
// 
//  Modified MIT License (MIT)
//  
//  Copyright (c) 2015 Completely Fair Games Ltd.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// The following content pieces are considered PROPRIETARY and may not be used
// in any derivative works, commercial or non commercial, without explicit 
// written permission from Completely Fair Games:
// 
// * Images (sprites, textures, etc.)
// * 3D Models
// * Sound Effects
// * Music
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using System.Runtime.Serialization;

namespace DwarfCorp
{
    public class LayeredCharacterSprite : CharacterSprite, IRenderableComponent, IUpdateableComponent
    {
        private class LayeredAnimationProxy : Animation
        {
            private LayeredCharacterSprite Owner = null;

            public LayeredAnimationProxy(LayeredCharacterSprite Owner)
            {
                this.Owner = Owner;
            }

            public override Texture2D GetTexture()
            {
                return Owner.GetProxyTexture();
            }

            public override void UpdatePrimitive(BillboardPrimitive Primitive, int CurrentFrame)
            {
                // Obviously shouldn't be hard coded.
                SpriteSheet = new SpriteSheet(Owner.GetProxyTexture(), 32, 40);
                base.UpdatePrimitive(Primitive, CurrentFrame);
            }

            public override bool CanUseInstancing { get => false; }
        }

        public Texture2D GetProxyTexture()
        {
            return Layers[0];
        }

        public override void AddAnimation(Animation animation)
        {
            var comp = animation as CompositeAnimation;
            var proxyAnim = new LayeredAnimationProxy(this)
            {
                Frames = comp == null ? animation.Frames : comp.CompositeFrames.Select(c => c.Cells[0].Tile).ToList(),
                Name = animation.Name,
                FrameHZ = 0.5f,
                Speeds = animation.Speeds,
                Tint = animation.Tint,
                Loops = animation.Loops
            };
            base.AddAnimation(proxyAnim);
        }

        public List<Texture2D> Layers = new List<Texture2D>();

        public LayeredCharacterSprite()
        {
            
        }

        public LayeredCharacterSprite(ComponentManager manager, string name,  Matrix localTransform) :
                base(manager, name, localTransform)
        {
        }

        new public void Update(DwarfTime gameTime, ChunkManager chunks, Camera camera)
        {
            base.Update(gameTime, chunks, camera);
        }

        new public void Render(DwarfTime gameTime, ChunkManager chunks, Camera camera, SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, Shader effect, bool renderingForWater)
        {
            //If layers have changed, re-create the texture.

            base.Render(gameTime, chunks, camera, spriteBatch, graphicsDevice, effect, renderingForWater);            
        }

        
    }
}