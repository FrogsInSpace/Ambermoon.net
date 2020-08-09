﻿/*
 * ICamera3D.cs - 3D camera interface
 *
 * Copyright (C) 2020  Robert Schneckenhaus <robert.schneckenhaus@web.de>
 *
 * This file is part of Ambermoon.net.
 *
 * Ambermoon.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * Ambermoon.net is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with Ambermoon.net. If not, see <http://www.gnu.org/licenses/>.
 */

namespace Ambermoon.Render
{
    public interface ICamera3D
    {
        Position Position { get; }

        /// <summary>
        /// This will reset the view angle to up
        /// </summary>
        void SetPosition(float x, float z);
        void MoveForward(float distance);
        void MoveBackward(float distance);
        void TurnLeft(float angle); // in degrees
        void TurnRight(float angle); // in degrees
        void TurnTowards(float angle); // turn to attacking monster or stand on a spinner (in degrees)
        void LevitateUp(float distance); // used for climbing up ladders/ropes or use levitation spell (distance is in the range of 0 to 1 where 1 is full room height)
        void LevitateDown(float distance); // used for climbing down ladders/ropes (distance is in the range of 0 to 1 where 1 is full room height)
        Position GetForwardPosition(float distance);
        Position GetBackwardPosition(float distance);
    }
}
