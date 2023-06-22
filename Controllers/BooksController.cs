using BookStoreApi.Models;
using BookStoreApi.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Cryptography;

namespace BookStoreApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BooksController : ControllerBase
{
    private readonly BooksService _booksService;

    public static byte[] GenerateMasterKey()
{
    using (var aes = Aes.Create())
    {
        aes.KeySize = 256;
        aes.GenerateKey();

        // Get the AES key
        var aesKey = aes.Key;

        // Create a new 96-byte array
        var masterKey = new byte[96];

        // Copy the first 96 bytes of the AES key to the master key
       if(aesKey.Length >= 96){ 
        Buffer.BlockCopy(aesKey, 0, masterKey, 0, 96);
        }
       else Buffer.BlockCopy(aesKey, 0, masterKey, 0, aesKey.Length);

          

        return masterKey;
    }
}
 

    public BooksController(BooksService booksService) =>
        _booksService = booksService;

    [HttpGet]
    public async Task<List<Book>> Get() =>
        await _booksService.GetAsync();

    // [HttpGet("{id:length(24)}")]
    // public async Task<ActionResult<Book>> Get(string id)
    // {
    //     var book = await _booksService.GetAsync(id);

    //     if (book is null)
    //     {
    //         return NotFound();
    //     }

    //     return book;
    // }
     
    [HttpGet("{id:length(2)}")]  
    public  string Get(string id ) {
// Usage:
var masterKey = GenerateMasterKey();
string base64MasterKey = Convert.ToBase64String(masterKey);
Console.WriteLine("Generated Master Key: " + base64MasterKey);

return base64MasterKey;
    }


    [HttpPost]
    public async Task<IActionResult> Post(Book newBook)
    {
        await _booksService.CreateAsync(newBook);

        return CreatedAtAction(nameof(Get), new { id = newBook.Id }, newBook);
    }

    [HttpPut("{id:length(24)}")]
    public async Task<IActionResult> Update(string id, Book updatedBook)
    {
        var book = await _booksService.GetAsync(id);

        if (book is null)
        {
            return NotFound();
        }

        updatedBook.Id = book.Id;

        await _booksService.UpdateAsync(id, updatedBook);

        return NoContent();
    }

    [HttpDelete("{id:length(24)}")]
    public async Task<IActionResult> Delete(string id)
    {
        var book = await _booksService.GetAsync(id);

        if (book is null)
        {
            return NotFound();
        }

        await _booksService.RemoveAsync(id);

        return NoContent();
    }
}