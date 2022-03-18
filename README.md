# ABA File Generator

## Overview
Generates an aba file for bulk banking transactions with Australian banks.
It's based on this repo: https://github.com/simonblee/aba-file-generator

## Support
.NET framework 4.7.2

## License
[MIT License](http://en.wikipedia.org/wiki/MIT_License)

## Usage
```csharp
using AbaFileGenerator;

var generator = new AbaFileGenerator
            {
                Bsb = "012-012", //ANZANZ E Trade Support
                AccountNumber = "12345678",
                BankName = "CBA",
                UserName = "Some name",
                Remitter = "From some guy",
                DirectEntryUserId = "999999",
                Description = "Payroll",
                IncludeAccountNumberInDescriptiveRecord = false
            };
var result = generator.Generate([transactions]);
if(result.IsValid){
    System.IO.File.WriteAllText("/my/aba/file.aba", resulit.Data.AbaString);
}
```

## References
- http://www.anz.com/Documents/AU/corporate/clientfileformats.pdf
- http://www.cemtexaba.com/aba-format/cemtex-aba-file-format-details.html
- https://github.com/mjec/aba/blob/master/sample-with-comments.aba
- https://www.fileactive.anz.com/filechecker
