using System;
using System.IO;


namespace SevenZipRadical.Compression.LZMA
{
    public static class SevenZipRadicalHelper
    {

       static int dictionary = 1 << 15;

      // static Int32 posStateBits = 2;
     // static  Int32 litContextBits = 3; // for normal files
        // UInt32 litContextBits = 0; // for 32-bit data
     // static  Int32 litPosBits = 0;
        // UInt32 litPosBits = 2; // for 32-bit data
    // static   Int32 algorithm = 2;
    // static    Int32 numFastBytes = 128;

     static   bool eos = false;





     static   CoderPropID[] propIDs = 
				{
					CoderPropID.DictionarySize,
					CoderPropID.PosStateBits,
					CoderPropID.LitContextBits,
					CoderPropID.LitPosBits,
					CoderPropID.Algorithm,
					CoderPropID.NumFastBytes,
					CoderPropID.MatchFinder,
					CoderPropID.EndMarker
				};

        // these are the default properties, keeping it simple for now:
     static   object[] properties = 
				{
					(Int32)(dictionary),
					(Int32)(2),
					(Int32)(3),
					(Int32)(0),
					(Int32)(2),
					(Int32)(128),
					"bt4",
					eos
				};


        public static byte[] Compress(byte[] inputBytes, ICodeProgress progress)
        {

            MemoryStream inStream = new MemoryStream(inputBytes);
            MemoryStream outStream = new MemoryStream();
            SevenZipRadical.Compression.LZMA.Encoder encoder = new SevenZipRadical.Compression.LZMA.Encoder();
            encoder.SetCoderProperties(propIDs, properties);
            encoder.WriteCoderProperties(outStream);
            long fileSize = dataProcessingSize = inStream.Length;
            for (int i = 0; i < 8; i++)
                outStream.WriteByte((Byte)(fileSize >> (8 * i)));
            encoder.Code(inStream, outStream, -1, -1, progress);
            return outStream.ToArray();
        }
		
		public static long dataProcessingSize;

        public static byte[] Decompress(byte[] inputBytes, ICodeProgress progress)
        {
            MemoryStream newInStream = new MemoryStream(inputBytes);

            SevenZipRadical.Compression.LZMA.Decoder decoder = new SevenZipRadical.Compression.LZMA.Decoder();
            
            newInStream.Seek(0, 0);
            MemoryStream newOutStream = new MemoryStream();

            byte[] properties2 = new byte[5];
            if (newInStream.Read(properties2, 0, 5) != 5)
                throw (new Exception("input .lzma is too short"));
            long outSize = 0;
            for (int i = 0; i < 8; i++)
            {
                int v = newInStream.ReadByte();
                if (v < 0)
                    throw (new Exception("Can't Read 1"));
                outSize |= ((long)(byte)v) << (8 * i);
            }
            decoder.SetDecoderProperties(properties2);

            long compressedSize = dataProcessingSize = newInStream.Length - newInStream.Position;
            decoder.Code(newInStream, newOutStream, compressedSize, outSize, progress);

            byte[] b = newOutStream.ToArray();

            return b;



        }


    }
}
