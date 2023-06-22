using BookStoreApi.Models;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Driver.Encryption;
using System.Security.Cryptography;
using System.Text.Json;

namespace BookStoreApi.Services
{
    public class BooksService
    {
        private const string __keyVault = "__keyVault";
        private const string provider = "local";
        private readonly IMongoCollection<Book> _booksCollection;

        private readonly IMongoCollection<Book> _encbooksCollection;
       // private readonly AutoEncrypter _autoEncrypter;

       //public static string fixedMasterKey = "M6blPmhQXS6hiqnGh+21diXL2jDmyue/HcHnFsnzG4Y=";
        //byte[] masterKey = Convert.FromBase64String(fixedMasterKey);
public static string fixedMasterKey = "M6blPmhQXS6hiqnGh+21diXL2jDmyue/HcHnFsnzG4Y=";
byte[] base64MasterKeyBytes = Convert.FromBase64String(fixedMasterKey);
byte[] masterKey = new byte[96];

 

      private ClientEncryption clientEncryption;

        

        public BooksService(IOptions<BookStoreDatabaseSettings> bookStoreDatabaseSettings)
        {
            var mongoClient = new MongoClient(bookStoreDatabaseSettings.Value.ConnectionString);

            var mongoDatabase = mongoClient.GetDatabase(bookStoreDatabaseSettings.Value.DatabaseName);

            _booksCollection = mongoDatabase.GetCollection<Book>(bookStoreDatabaseSettings.Value.BooksCollectionName);

            

            // var keyVaultClient = new MongoClient(bookStoreDatabaseSettings.Value.ConnectionString);
            // var options = new ClientEncryptionOptions(
            //     mongoClient,
            //     new CollectionNamespace(bookStoreDatabaseSettings.Value.DatabaseName, __keyVault),
            //     kmsProviders,
            //     new Dictionary<string, SslSettings>()
            // );

            // var keyVaultNamespace = new CollectionNamespace(bookStoreDatabaseSettings.Value.DatabaseName, __keyVault).ToBsonDocument();
            // options.GetType().GetProperty("KeyVaultNamespace").SetValue(options, keyVaultNamespace, null); // Set the value of the KeyVaultNamespace property

            // var encryptionOptions = new EncryptionOptions(
            //     keyVaultClient,
            //     options
            // );

            //_autoEncrypter = new AutoEncrypter(encryptionOptions);
             if (base64MasterKeyBytes.Length >= 96)
                {
                    // Copy the first 96 bytes of the base64 master key to the master key array
                    Buffer.BlockCopy(base64MasterKeyBytes, 0, masterKey, 0, 96);
                }
                else
                {
                    // Pad the master key array with zeros if the base64 master key is shorter than 96 bytes
                    Buffer.BlockCopy(base64MasterKeyBytes, 0, masterKey, 0, base64MasterKeyBytes.Length);
                }

             var kmsProviders = new Dictionary<string, IReadOnlyDictionary<string, object>>
            {
                ["local"] = new Dictionary<string, object>
                {
                    ["key"] = Convert.ToBase64String(masterKey),
                }
            };

            var clientSettings = MongoClientSettings.FromConnectionString(bookStoreDatabaseSettings.Value.ConnectionString);
            var autoEncryptionOptions = new AutoEncryptionOptions(
                keyVaultNamespace:  new CollectionNamespace(bookStoreDatabaseSettings.Value.DatabaseName , __keyVault),
                kmsProviders: kmsProviders,
                bypassAutoEncryption: true);
            clientSettings.AutoEncryptionOptions = autoEncryptionOptions;
            var client = new MongoClient(clientSettings);
             _encbooksCollection = client.GetDatabase(bookStoreDatabaseSettings.Value.DatabaseName).GetCollection<Book>(bookStoreDatabaseSettings.Value.BooksCollectionName);
          

            
            var  clientEncryptionOptions = new ClientEncryptionOptions(
                keyVaultClient: client,
                keyVaultNamespace:  new CollectionNamespace(bookStoreDatabaseSettings.Value.DatabaseName , __keyVault),
                kmsProviders: kmsProviders);
             clientEncryption = new ClientEncryption(clientEncryptionOptions);
 
            // var dataKeyOptions = new DataKeyOptions();
            // var dataKeyId = clientEncryption.CreateDataKey(provider, dataKeyOptions, CancellationToken.None);
            // var dataKeyIdBase64 = Convert.ToBase64String(GuidConverter.ToBytes(dataKeyId, GuidRepresentation.Standard));
            // Console.WriteLine($"DataKeyId [base64]: {dataKeyIdBase64}");

        }

        public async Task<List<Book>> GetAsync()
        {   
           var encryptedBooks = await _encbooksCollection.Find(_ => true).ToListAsync();
            

    var decryptedBooks = new List<Book>();

    foreach (var encryptedBook in encryptedBooks)
    {
        var decryptedBook = new Book();
        decryptedBook.Id = encryptedBook.Id;
        decryptedBook.BookName = encryptedBook.BookName;
        decryptedBook.Category = encryptedBook.Category;
        decryptedBook.Author = encryptedBook.Author;

                // Decrypt the encrypted fields
                if(encryptedBook.NewPrice != null){
                BsonValue bsonValue = clientEncryption.Decrypt(
                            encryptedBook.NewPrice.AsBsonBinaryData,
                            CancellationToken.None);
                decryptedBook.Price = bsonValue.ToDecimal();
                }
               

        decryptedBooks.Add(decryptedBook);
    }

    

    return decryptedBooks;

            //return await _encbooksCollection.Find(_ => true).ToListAsync();
        }

        public async Task<Book?> GetAsync(string id)
        {
            return await _booksCollection.Find(x => x.Id == id).FirstOrDefaultAsync();
        }

        public async Task CreateAsync(Book newBook)
        {
            // Encrypt the Price field
            //var encryptedBook = _autoEncrypter.Encrypt(newBook);

            //await _booksCollection.InsertOneAsync(newBook); 
            
            var dataKeyOptions = new DataKeyOptions();
            var dataKeyId = clientEncryption.CreateDataKey(provider, dataKeyOptions, CancellationToken.None);

      var encryptedPrice = clientEncryption.Encrypt(
    "24.34",
    new EncryptOptions(algorithm: "AEAD_AES_256_CBC_HMAC_SHA_512-Deterministic", keyId: dataKeyId),
    CancellationToken.None);

    Book book = new Book
{   Id = newBook.Id,
    Price = newBook.Price,
    BookName = newBook.BookName,
    NewPrice = encryptedPrice,
    Author = newBook.Author
};

             await _encbooksCollection.InsertOneAsync(book);
             
        }

        public async Task UpdateAsync(string id, Book updatedBook)
        {
            await _booksCollection.ReplaceOneAsync(x => x.Id == id, updatedBook);
        }

        public async Task RemoveAsync(string id)
        {
            await _booksCollection.DeleteOneAsync(x => x.Id == id);
        }
    }
}
