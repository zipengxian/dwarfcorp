using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;

namespace DwarfCorp.Gui.Widgets
{
    public class InfoTicker : Gui.Widget
    {
        private List<String> Messages = new List<String>();
        private global::System.Threading.Mutex MessageLock = new global::System.Threading.Mutex();
        private bool NeedsInvalidated = false;

        public Vector4 TextBackgroundColor = new Vector4(0.0f, 0.0f, 0.0f, 0.25f);

        public int VisibleLines
        {
            get
            {
                var font = Root.GetTileSheet(Font);
                return Rect.Height / (font.TileHeight * TextSize);
            }
        }

        public override void Construct()
        {
            Root.RegisterForUpdate(this);

            OnUpdate = (sender, time) =>
            {
                MessageLock.WaitOne();
                if (NeedsInvalidated)
                    this.Invalidate();
                NeedsInvalidated = false;
                MessageLock.ReleaseMutex();
            };
        }

        public void AddMessage(String Message)
        {
            // AddMessage is called by another thread - need to protect the list.
            MessageLock.WaitOne();

            if (Message.StartsWith("#"))
            {
                if (Messages.Count > 0) Messages.RemoveAt(Messages.Count - 1);
                Messages.Add(Message.Substring(1));
            }
            else
                Messages.Add(Message);

            if (Messages.Count > VisibleLines)
                Messages.RemoveAt(0);
            // Need to invalidate inside the main GUI thread or else!
            NeedsInvalidated = true;
            MessageLock.ReleaseMutex();
        }

        protected override Gui.Mesh Redraw()
        {
            var mesh = Mesh.EmptyMesh();
            var font = Root.GetTileSheet(Font);
            var basic = Root.GetTileSheet("basic");
            var linePos = 0;

            MessageLock.WaitOne();
            foreach (var line in Messages)
            {
                var stringSize = Mesh.MeasureStringMesh(line, font, new Vector2(TextSize, TextSize));

                mesh.QuadPart()
                    .Scale(stringSize.Width, stringSize.Height)
                    .Translate(Rect.X, Rect.Y + linePos)
                    .Texture(basic.TileMatrix(1))
                    .Colorize(TextBackgroundColor);

                mesh.StringPart(line, font, new Vector2(TextSize, TextSize), out var _)
                    .Translate(Rect.X, Rect.Y + linePos)
                    .Colorize(TextColor);

                linePos += font.TileHeight * TextSize;
            }
            MessageLock.ReleaseMutex();

            return mesh;
        }

        public bool HasMesssage(string loadingMessage)
        {
            return Messages.Contains(loadingMessage);
        }
    }
}
