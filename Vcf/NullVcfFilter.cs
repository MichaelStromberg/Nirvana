﻿using System.IO;
using Genome;

namespace Vcf
{
    public sealed class NullVcfFilter : IVcfFilter
    {

        public void FastForward(StreamReader reader)
        {
        }

        public string GetNextLine(StreamReader reader) => reader.ReadLine();

        public bool PassedTheEnd(IChromosome chromosome, int position) => false;
    }
}