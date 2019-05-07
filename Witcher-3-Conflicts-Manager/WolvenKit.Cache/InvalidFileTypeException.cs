using System;

namespace WolvenKit.Cache
{
    public class InvalidCacheException : Exception
    {
        public InvalidCacheException(string message) : base(message)
        {
        }
    }
}