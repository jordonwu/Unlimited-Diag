using System;

public class Virtual_Memory
{
    List<byte[]> blob_list;
    List<blob_attributes> legend;

	public Virtual_Memory()
	{
        blob_list = new List<byte[]>();
        legend = new List<blob_attributes>();
	}

    public byte Read(int Address)
    {

    }

    public byte [] Read(int Address, int Length)
    {

    }

    public void Write(int Address, byte Data)
    {

    }

    public void Write(int Address, byte [] Data)
    {

    }
    private struct blob_attributes
    {
        int blob_index;
        int start;
        int end;
        int length;
    }
}
