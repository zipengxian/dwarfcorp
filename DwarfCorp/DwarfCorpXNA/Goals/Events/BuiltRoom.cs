﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DwarfCorp.Goals.Events
{
    public class BuiltRoom : GameEvent
    {
        public String RoomType;

        public BuiltRoom(String Type)
        {
            RoomType = Type;
        }
    }
}
