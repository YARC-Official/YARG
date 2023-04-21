using System;
using System.IO;

namespace DtxCS
{
  /// <summary>
  /// Provides a means to decrypt an encrypted DTB file (or any other file using this crypt method).
  /// </summary>
  public class CryptStream : Stream
  {
    private long position;
    private readonly int key;
    private int curKey;
    private long keypos;
    private Stream file;

    public CryptStream(Stream file, bool write = false)
    {
      file.Position = 0;
      if (write)
      {
        int rKey = new Random().Next();
        file.WriteInt32LE(rKey);
        this.key = cryptRound(rKey);
      }
      else
        this.key = cryptRound(file.ReadInt32LE());
      this.curKey = this.key;
      this.file = file;
      this.Length = file.Length - 4;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length { get; }

    public override long Position
    {
      get
      {
        return position;
      }

      set
      {
        Seek(value, SeekOrigin.Begin);
      }
    }

    private void updateKey()
    {
      if (keypos == position)
        return;
      if (keypos > position) // reset key (TODO: is there a better way to "re-wind" the key?)
      {
        keypos = 0;
        curKey = key;
      }
      while (keypos < position)
      {
        curKey = cryptRound(curKey);
        keypos++;
      }
    }

    private int cryptRound(int key)
    {
      int ret = (key - ((key / 0x1F31D) * 0x1F31D)) * 0x41A7 - (key / 0x1F31D) * 0xB14;
      if (ret <= 0)
        ret += 0x7FFFFFFF;
      return ret;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
      // ensure file is at correct offset
      file.Seek(this.position + 4, SeekOrigin.Begin);
      if (offset + count > buffer.Length)
      {
        throw new IndexOutOfRangeException("Attempt to fill buffer past its end");
      }
      if (this.Position == this.Length || this.Position + count > this.Length)
      {
        count = (int)(this.Length - this.Position);
        //throw new System.IO.EndOfStreamException("Cannot read past end of file.");
      }

      int bytesRead = file.Read(buffer, offset, count);

      for (uint i = 0; i < bytesRead; i++)
      {
        buffer[offset + i] ^= (byte)(this.curKey);
        this.position++;
        updateKey();
      }
      return bytesRead;
    }


    public override void Write(byte[] buffer, int offset, int count)
    {
      // ensure file is at correct offset
      file.Seek(this.position + 4, SeekOrigin.Begin);
      if (offset + count > buffer.Length)
      {
        throw new IndexOutOfRangeException("Attempt to fill buffer past its end");
      }

      byte[] tempBuf = new byte[count];
      for (uint i = 0; i < count; i++)
      {
        tempBuf[offset + i] = (byte)(buffer[offset + i] ^ (byte)(this.curKey));
        this.position++;
        updateKey();
      }

      file.Write(tempBuf, offset, count);
      return;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
      int adjust = origin == SeekOrigin.Current ? 0 : 4;
      this.position = file.Seek(offset + adjust, origin) - 4;
      updateKey();
      return position;
    }

    #region Not Used

    public override void Flush()
    {
      throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
      throw new NotSupportedException();
    }

    #endregion
  }
}
