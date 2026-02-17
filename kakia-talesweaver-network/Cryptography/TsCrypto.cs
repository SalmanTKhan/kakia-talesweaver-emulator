using Yggdrasil.Util;

namespace kakia_talesweaver_network.Cryptography;

public class TsCrypto
{
	// Generate key bytes from a key seed and a local file 'keyblob.bin'
	public static byte[] GenKey(uint keySeed)
	{
		byte[] keyblobRaw = Array.Empty<byte>();

		if (!File.Exists("keyblob.bin"))
			throw new FileNotFoundException("keyblob.bin not found.");

		keyblobRaw = File.ReadAllBytes("keyblob.bin");

		if (keyblobRaw.Length > 16)
		{
			string keyBodySig = Hex.ToString(keyblobRaw, 12, 4);
			Console.WriteLine($"[TwCrypto] Key Raw. Seed: {keySeed:X8}, KeySig: {keyBodySig} (First 4 bytes of body)");
		}

		int size = (((int)(keySeed >> 0x14) ^ ((int)(keySeed >> 8) & 0xff)) & 0xf) ^ ((int)(keySeed >> 0x14) & 0xff);
		int offset = (((int)(keySeed >> 0xc) & 0xF00) | ((int)(keySeed >> 4) & 0xF) | ((int)(keySeed >> 8) & 0xF0) | ((int)(keySeed >> 0x10) & 0xF000));

		if (offset < 0) offset = 0;
		if (size <= 0 || offset >= keyblobRaw.Length)
		{
			// fallback: use some bytes from blob or zeros
			size = Math.Min(Math.Max(size, 0), keyblobRaw.Length - offset);
		}

		// ensure we don't go out of range
		if (offset + size > keyblobRaw.Length)
			size = Math.Max(0, keyblobRaw.Length - offset);

		byte[] keyblob = new byte[size];
		Array.Copy(keyblobRaw, offset, keyblob, 0, size);

		int key_size = keyblob.Length;
		// initialize kout0..255
		byte[] kout = new byte[256];
		for (int i = 0; i < 256; i++) kout[i] = (byte)i;
		int j = 0;
		if (key_size == 0) key_size = 1; // avoid div by zero
		for (int i = 0; i < 256; i++)
		{
			j = (j + kout[i] + keyblob[i % key_size]) % 256;
			// swap kout[i], kout[j]
			byte tmp = kout[i];
			kout[i] = kout[j];
			kout[j] = tmp;
		}

		// prepend header (12 bytes)
		byte[] header = new byte[] { 0x02, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x01, 0x01, 0x00 };
		byte[] result = new byte[header.Length + kout.Length];
		Array.Copy(header, 0, result, 0, header.Length);
		Array.Copy(kout, 0, result, header.Length, kout.Length);

		if (result.Length > 12)
		{
			result[11] = result[12];
		}

		if (result.Length > 16)
		{
			string keyBodySig = Hex.ToString(result, 12, 4);
			Console.WriteLine($"[TsCrypto] Key Gen. Seed: {keySeed:X8}, KeySig: {keyBodySig} (First 4 bytes of body)");
		}

		return result;
	}

	public static byte[] Encrypt(byte[] key, byte[] packetBuffIn, int sendIndex)
	{
		int packetLen = packetBuffIn.Length;
		byte[] packetBuffOut = new byte[packetLen];
		int loop_index = 1;
		int v12 = key[11];

		for (int i = 0; i < packetLen; i++)
		{
			v12 ^= packetBuffIn[i];
			int v9 = (key[(loop_index + 12) % key.Length] ^ v12) & 0xFF;
			int v7 = (loop_index + 1) & 0xFF;
			int v10 = (key[(v7 + 12) % key.Length] + v9) & 0xFF;
			loop_index = (v7 + 1) & 0xFF;

			int j = 1;
			while ((key[0] & 0xFF) > j)
			{
				int v11 = (key[(loop_index + 12) % key.Length] ^ v10) & 0xFF;
				int v8 = (loop_index + 1) & 0xFF;
				v10 = (key[(v8 + 12) % key.Length] + v11) & 0xFF;
				loop_index = (v8 + 1) & 0xFF;
				j++;
			}

			packetBuffOut[i] = (byte)v10;
		}

		byte[] finalPacket = new byte[packetLen + 1];
		finalPacket[0] = (byte)(sendIndex & 0xFF);

		Array.Copy(packetBuffOut, 0, finalPacket, 1, packetBuffOut.Length);
		return finalPacket;
	}

	public static byte[]? Decrypt(byte[] key, byte[] encPack)
	{
		if (encPack == null || encPack.Length < 4) return null;
		if (encPack[0] != 0xAA) return null;
		int packetLength = (encPack[1] << 8) | encPack[2];
		if (packetLength <= 0) return null;
		int seq = encPack[3];
		int dataLen = packetLength - 1;
		if (4 + dataLen > encPack.Length) return null;
		byte[] packetBuffIn = new byte[dataLen];
		Array.Copy(encPack, 4, packetBuffIn, 0, dataLen);

		// --- DEBUG LOGGING ---
		//Console.WriteLine($"[Crypto] In: {Hex.ToString(packetBuffIn)}");
		// ---------------------

		int packet_len = packetBuffIn.Length;
		byte[] packetBuffOut = new byte[packet_len];

		int key0 = key[0];
		int xorSize = 16 * ((key0 + 30) >> 4);
		if (xorSize <= 0) xorSize = 16;
		byte[] xor_buf1 = new byte[xorSize];
		byte[] xor_buf2 = new byte[xorSize];
		int temp_byte1 = 1;
		int temp_byte2 = key[11];

		for (int i = 0; i < packet_len; i++)
		{
			// build xor buffers
			for (int j = 0; j < key0; j++)
			{
				xor_buf2[j] = key[(temp_byte1 + 12) % key.Length];
				temp_byte1 = (temp_byte1 + 1) & 0xFF;
				xor_buf1[j] = key[(temp_byte1 + 12) % key.Length];
				temp_byte1 = (temp_byte1 + 1) & 0xFF;
			}

			int temp_byte3 = packetBuffIn[i];

			int xor_ptr_offset = key[4];
			int j2 = key[4];
			// process in reverse
			while (j2 > 0)
			{
				int a = xor_buf1[xor_ptr_offset + j2 - 1];
				int b = xor_buf2[xor_ptr_offset + j2 - 1];
				// subtract a
				temp_byte3 = (temp_byte3 - a) & 0xFF;
				// xor with b
				temp_byte3 = (temp_byte3 ^ b) & 0xFF;
				j2--;
			}

			// subtract xor_buf1[0]
			temp_byte3 = (temp_byte3 - xor_buf1[0]) & 0xFF;

			int mix = (xor_buf2[0] ^ temp_byte2) & 0xFF;
			temp_byte3 = (temp_byte3 ^ mix) & 0xFF;

			packetBuffOut[i] = (byte)temp_byte3;
			temp_byte2 ^= temp_byte3;
		}

		// --- DEBUG LOGGING ---
		//Console.WriteLine($"[Crypto] Out: {Hex.ToString(packetBuffOut)}");
		// ---------------------

		return packetBuffOut;
	}
}
