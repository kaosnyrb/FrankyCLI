using System.Collections.Generic;

namespace Retrograde.AI.Utils
{
    public record ElevenLabsVoice(string Name, string Id);

    public static class VoiceSeedData
    {
        public static readonly List<ElevenLabsVoice> FemaleVoices = new()
        {
            new("Ada", "ClKfJnuqp0hQ7Ax41F4w"),
            new("Megan", "E393dkE75hqtz1LO2aEJ"),
            new("Jessica Gallagher", "DbwWo4rVEd5NrejHYUnm"),
            new("Emily", "odyUrTN5HMVKujvVAgWW"),
            new("Isla", "h8eW5xfRUGVJrZhAFxqK"),
            new("Lauren", "l4Coq6695JDX9xtLqXDE"),
            new("Caty", "54Cze5LrTSyLgbO6Fhlc"),
            new("Serafina", "4tRn1lSkEn13EVTuqb0g"),
            new("Xiaoxi", "rk9BD4xwuG39syvDIBQy"),
            new("Sara", "BXeZVLBk6Y5kMHj9cDEY"),
        };

        public static readonly List<ElevenLabsVoice> MaleVoices = new()
        {
            new("Hunter", "inKOuEy40NdNVHxcqekZ"),
            new("Royce", "omRordDZNt4Gy45cetUa"),
            new("David Jarrett", "CjIKi1zuI666pVsFrtyU"),
            new("Simon Aus", "zcIk2xc7SGwlywr4TzZu"),
            new("Julian Aus", "7QTeMqXOsYj1AQaKLcrf"),
            new("Tristan", "CsbTapRVtZdcBs3vBbQe"),
            new("Hector", "dgkKQcJqyy5AP0dqleUU"),
            new("JonJebus ", "wsHauqjSkdBeAvdbUFmR"),
        };
    }
}
