﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System;
using FASTER.core;
using NUnit.Framework;

namespace FASTER.test
{
    [TestFixture]
    internal class DeltaLogStandAloneTests
    {
        private FasterLog log;
        private IDevice device;
        private string path;

        [SetUp]
        public void Setup()
        {
            path = TestUtils.MethodTestDir + "/";

            // Clean up log files from previous test runs in case they weren't cleaned up
            TestUtils.DeleteDirectory(path, wait: true);
        }

        [TearDown]
        public void TearDown()
        {
            log?.Dispose();
            log = null;
            device?.Dispose();
            device = null;

            // Clean up log files
            TestUtils.DeleteDirectory(path, wait: true);
        }

        [Test]
        [Category("FasterLog")]
        [Category("Smoke")]
        public void DeltaLogTest1([Values] TestUtils.DeviceType deviceType)
        {
            const int TotalCount = 200; 
            string filename = $"{path}delta_{deviceType}.log";
            TestUtils.RecreateDirectory(path);

            device = TestUtils.CreateTestDevice(deviceType, filename);
            device.Initialize(-1);
            using DeltaLog deltaLog = new DeltaLog(device, 12, 0);
            Random r = new (20);
            int i;

            SectorAlignedBufferPool bufferPool = new(1, (int)device.SectorSize);
            deltaLog.InitializeForWrites(bufferPool);
            for (i = 0; i < TotalCount; i++)
            {
                int _len = 1 + r.Next(254);
                long address;
                while (true)
                {
                    deltaLog.Allocate(out int maxLen, out address);
                    if (_len <= maxLen) break;
                    deltaLog.Seal(0);
                }
                for (int j = 0; j < _len; j++)
                {
                    unsafe { *(byte*)(address + j) = (byte)_len; }
                }
                deltaLog.Seal(_len, i);
            }
            deltaLog.FlushAsync().Wait();

            deltaLog.InitializeForReads();
            r = new (20);
            for (i = 0; deltaLog.GetNext(out long address, out int len, out int type); i++)
            {
                int _len = 1 + r.Next(254);
                Assert.AreEqual(i, type);
                Assert.AreEqual(len, _len);
                for (int j = 0; j < len; j++)
                {
                    unsafe { Assert.AreEqual((byte)_len, *(byte*)(address + j)); };
                }
            }
            Assert.AreEqual(TotalCount, i, $"i={i} and TotalCount={TotalCount}");
            bufferPool.Free();
        }
    }
}
