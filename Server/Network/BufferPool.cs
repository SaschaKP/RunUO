/***************************************************************************
 *                               BufferPool.cs
 *                            -------------------
 *   begin                : May 1, 2002
 *   copyright            : (C) The RunUO Software Team
 *   email                : info@runuo.com
 *
 *   $Id$
 *
 ***************************************************************************/

/***************************************************************************
 *
 *   This program is free software; you can redistribute it and/or modify
 *   it under the terms of the GNU General Public License as published by
 *   the Free Software Foundation; either version 2 of the License, or
 *   (at your option) any later version.
 *
 ***************************************************************************/

using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;

namespace Server.Network
{
    public class BufferPool
    {
        private static readonly List<BufferPool> m_Pools = new List<BufferPool>();
        public static List<BufferPool> Pools => m_Pools;

        private readonly ConcurrentStack<byte[]> m_FreeBuffers = new();

        private readonly string m_Name;
        private readonly int m_InitialCapacity;
        private readonly int m_BufferSize;
        private int m_Misses;
        private int m_CurrentCreated; // Tracking of created arrays

        public BufferPool(string name, int initialCapacity, int bufferSize)
        {
            m_Name = name;
            m_InitialCapacity = initialCapacity;
            m_BufferSize = bufferSize;
            m_CurrentCreated = initialCapacity;

            // Pre-allocate valid contiguous memory array: One single block to avoid LOH
            for (int i = 0; i < initialCapacity; ++i)
            {
                m_FreeBuffers.Push(new byte[bufferSize]);
            }

            lock (m_Pools) { m_Pools.Add(this); }
        }

        public byte[] AcquireBuffer()
        {
            if (m_FreeBuffers.TryPop(out byte[] buffer))
            {
                return buffer;
            }

            // Increment misses and create a new buffer
            Interlocked.Increment(ref m_Misses);
            Interlocked.Increment(ref m_CurrentCreated);

            return new byte[m_BufferSize];
        }

        public void ReleaseBuffer(byte[] buffer)
        {
            // Don't allow null or external buffers here
            if (buffer == null || buffer.Length != m_BufferSize)
                return;

            m_FreeBuffers.Push(buffer);
        }

        public void GetInfo(out string name, out int freeCount, out int initialCapacity, out int currentCapacity, out int bufferSize, out int misses)
        {
            name = m_Name;
            freeCount = m_FreeBuffers.Count;
            initialCapacity = m_InitialCapacity;
            currentCapacity = Volatile.Read(ref m_CurrentCreated);
            bufferSize = m_BufferSize;
            misses = Volatile.Read(ref m_Misses);
        }

        public void Free()
        {
            lock (m_Pools) { m_Pools.Remove(this); }
            m_FreeBuffers.Clear();
        }
    }
}
