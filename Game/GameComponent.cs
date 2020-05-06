﻿using PIGMServer.Game.Systems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PIGMServer.Game
{
    public abstract class GameComponent : IGameComponent
    {
        public      string      Name            { get; private set; }
        public      bool        Altered =       false;
        public bool             IsJustCreated   { get; private set; }
        public      GameEntity  Parent;

        public GameComponent(GameEntity parent)
        {
            Parent = parent;
            Name = parent.Name;
            IsJustCreated = true;
        }
        private void GenerateName()
        {
            Guid id = Guid.NewGuid();
            Name = id.ToString();
        }

        public bool IsAltered()
        {
            bool altered = Altered;
            Altered = false;

            return altered;
        }
        public GameEntity GetParent => Parent;
        public abstract SystemTypes GetSystem();

        public string GetName()
        {
            return Name;
        }

        public void IsOldNow()
        {
            IsJustCreated = false;
        }

        public bool JustCreated()
        {
            return IsJustCreated;
        }
    }
}
