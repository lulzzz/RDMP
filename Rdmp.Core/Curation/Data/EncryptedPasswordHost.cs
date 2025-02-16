// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using Rdmp.Core.Repositories;
using ReusableLibraryCode.DataAccess;

namespace Rdmp.Core.Curation.Data
{
    /// <summary>
    /// Helper class for becomming an IEncryptedPasswordHost via SimpleStringValueEncryption.  This class needs an ICatalogueRepository because
    /// SimpleStringValueEncryption is only secure when there is a private RSA encryption key specified in the CatalogueRepository.  This key 
    /// certificate will be a file location.  This allows you to use windows file system based user authentication to securely encrypt strings
    /// within RDMP databases.
    /// 
    /// <para>See also PasswordEncryptionKeyLocationUI</para>
    /// </summary>
    public class EncryptedPasswordHost : IEncryptedPasswordHost
    {
        /// <summary>
        /// This is only to support XML de-serialization
        /// </summary>
        internal class FakeEncryptedString : IEncryptedString
        {
            public string Value { get; set; }
            public string GetDecryptedValue()
            {
                throw new System.NotImplementedException();
            }

            public bool IsStringEncrypted(string value)
            {
                throw new System.NotImplementedException();
            }
        }

        private readonly IEncryptedString _encryptedString;

        /// <summary>
        /// For XML serialization
        /// </summary>
        protected EncryptedPasswordHost()
        {
            // This is to get around the issue where during de-serialization we cannot create an EncryptedString because there is no access to a repository.
            // If there is not a valid _encryptedString then de-serialization will fail (_encryptedString.Value is needed).
            // This provides an implementation of IEncryptedString which is only valid for deserializing the encrypted password from an XML representation and providing the encrypted password to a 'real' EncryptedPasswordHost
            _encryptedString = new FakeEncryptedString();
        }

        /// <summary>
        /// Prepares the object for decrypting/encrypting passwords based on the <see cref="Repositories.Managers.PasswordEncryptionKeyLocation"/>
        /// </summary>
        /// <param name="repository"></param>
        public EncryptedPasswordHost(ICatalogueRepository repository)
        {
            _encryptedString = new EncryptedString(repository);
        }

        /// <inheritdoc/>
        public string Password
        {
            get
            {
                return _encryptedString.Value;
            }
            set
            {
                _encryptedString.Value = value;
            }
        }

        /// <inheritdoc/>
        public string GetDecryptedPassword()
        {
            return _encryptedString.GetDecryptedValue();
        }
    }
}
