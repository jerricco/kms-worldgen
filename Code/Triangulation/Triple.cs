using System.Collections;

namespace Sandbox.Triangulation;

public struct Triple<T> : IEnumerable<T>
{
	public T Item1;
	public T Item2;
	public T Item3;

	public static Triple<T> Create(T item1, T item2, T item3)
	{
		return new Triple<T>(item1, item2, item3);
	}

	public Triple(T item1, T item2, T item3)
	{
		this.Item1 = item1;
		this.Item2 = item2;
		this.Item3 = item3;
	}

	public IEnumerator<T> GetEnumerator()
	{
		yield return this.Item1;
		yield return this.Item2;
		yield return this.Item3;
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		yield return this.Item1;
		yield return this.Item2;
		yield return this.Item3;
	}

	public static implicit operator (T, T, T)(Triple<T> t)
	{
		return (t.Item1, t.Item2, t.Item3);
	}
	public static implicit operator Triple<T>((T, T, T) t)
	{
		return Create(t.Item1, t.Item2, t.Item3);
	}
}
