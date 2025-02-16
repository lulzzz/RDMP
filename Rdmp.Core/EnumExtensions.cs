﻿// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using Rdmp.Core.DataLoad.Triggers;

namespace Rdmp.Core
{
    public static class EnumExtensions
    {
        /// <summary>
        /// Returns a culture specific string for the <see cref="Enum"/>
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public static string S(this Enum e)
        {
            if(e is TriggerStatus ts)
                switch (ts)
                {
                    case TriggerStatus.Enabled:
                        return GlobalStrings.Enabled;
                    case TriggerStatus.Disabled:
                        return GlobalStrings.Disabled;
                    case TriggerStatus.Missing:
                        return GlobalStrings.Missing;
                    default:
                        throw new ArgumentOutOfRangeException();
                }


            return e.ToString();
        }
    }
}
