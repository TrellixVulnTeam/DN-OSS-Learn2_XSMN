﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient.SNI;

namespace Microsoft.Data.SqlClient
{
    internal sealed class TdsParserStateObjectFactory
    {

        public static bool UseManagedSNI => true;

        public static readonly TdsParserStateObjectFactory Singleton = new TdsParserStateObjectFactory();

        public EncryptionOptions EncryptionOptions => SNI.SNILoadHandle.SingletonInstance.Options;

        public uint SNIStatus => SNI.SNILoadHandle.SingletonInstance.Status;

        /// <summary>
        /// Verify client encryption possibility.
        /// </summary>
        public bool ClientOSEncryptionSupport => SNI.SNILoadHandle.SingletonInstance.ClientOSEncryptionSupport;

        public TdsParserStateObject CreateTdsParserStateObject(TdsParser parser)
        {
            return new TdsParserStateObjectManaged(parser);
        }

        internal TdsParserStateObject CreateSessionObject(TdsParser tdsParser, TdsParserStateObject _pMarsPhysicalConObj, bool v)
        {
            return new TdsParserStateObjectManaged(tdsParser, _pMarsPhysicalConObj, true);
        }
    }
}
