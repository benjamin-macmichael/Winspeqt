using System;
using System.Diagnostics;

namespace Winspeqt.Models
{
    public sealed class ProcessInfo
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
        
        public long Memory { get; init; } = 0;
    }
}