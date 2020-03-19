﻿using PIGMServer.Game.Components;
using PIGMServer.Game.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PIGMServer.Game.Systems
{
    public class MoveableSystem : GameSystem<Movable>
    {
        public MoveableSystem(string worldName) : base(worldName)
        { }

        protected override void Process(Movable component, float deltaTime)
        {
            Vector2 position = component.Parent.Position;
            Vector2 velocity = component.Direction;

            component.Parent.Position = position + (position * (velocity * deltaTime));
        }
        public override SystemTypes GetSystemType()
        {
            return SystemTypes.Movable;
        }
    }
}
