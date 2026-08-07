using System;

namespace Sandbox.Generation;

public sealed class RevealRadius
{
	private int _chunkSize;
	
	public RevealRadius( int chunkSize = 50 )
	{
		_chunkSize = chunkSize;
	}
	
	public void StreamChunksInRadius() {} // Stream all areas enclosing chunks to the MapGenerator
	// Begin rendering the completed chunk. If the last in the stream,
	// check if marked for despawn and despawn if so.
	public void OnChunkComplete() { } 
	
	/// <summary>
    /// Scans the circular field and yields the top-left global space coordinate of every 
    /// grid-aligned chunk that fits completely inside the radius. This function searches grid space
    /// </summary>
    public IEnumerable<Vector2> EnumerateChunksInside( Vector2 center, int radius = 4 )
    {
	    int squareRadius = radius * radius;
	    // calculate boundaries purely in CHUNK INDEX coordinates
	    int minChunkX = (int)center.x - radius;
	    int maxChunkX = (int)center.x + radius;
	    int minChunkY = (int)center.y - radius;
	    int maxChunkY = (int)center.y + radius;

        Log.Warning( $"Chunk box created around {minChunkX},{minChunkY} to {maxChunkX},{maxChunkY} in chunk space" );
        
        // loop through chunk indices
        for ( int cx = minChunkX; cx <= maxChunkX; cx++ )
        {
	        for ( int cy = minChunkY; cy <= maxChunkY; cy++ )
	        {
		        // Define the 4 corners of this chunk in chunk space
		        float left = cx;
		        float right = cx + 1;
		        float top = cy;
		        float bottom = cy - 1;

		        // strict containment check (Compare chunk index distances directly to index radius)
		        float dxCenter = center.x;
		        float dyCenter = center.y;

		        bool topLeftIn = ((left - dxCenter) * (left - dxCenter)) 
									+ ((top - dyCenter) * (top - dyCenter)) <= squareRadius;
		        bool topRightIn = ((right - dxCenter) * (right - dxCenter)) 
									+ ((top - dyCenter) * (top - dyCenter)) <= squareRadius;
		        bool bottomLeftIn = ((left - dxCenter) * (left - dxCenter)) 
		                            + ((bottom - dyCenter) * (bottom - dyCenter)) <= squareRadius;
		        bool bottomRightIn = ((right - dxCenter) * (right - dxCenter)) 
									+ ((bottom - dyCenter) * (bottom - dyCenter)) <= squareRadius;

		        // if all four corners fit inside the index radius, convert to global world coordinates and yield
		        if ( topLeftIn && topRightIn && bottomLeftIn && bottomRightIn )
		        {
			        float globalX = cx * _chunkSize;
			        float globalY = cy * _chunkSize;
			        yield return new Vector2( globalX, globalY );
		        }
	        }
        }
    }
}
