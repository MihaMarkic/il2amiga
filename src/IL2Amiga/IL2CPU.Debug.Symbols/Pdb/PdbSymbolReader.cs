// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace IL2CPU.Debug.Symbols.Pdb
{
    /// <summary>
    /// Abstraction for reading Pdb files
    /// </summary>
    public abstract class PdbSymbolReader : IDisposable
    {
        public abstract IEnumerable<ILSequencePoint> GetSequencePointsForMethod(int methodToken);
        public abstract void Dispose();
    }
}
