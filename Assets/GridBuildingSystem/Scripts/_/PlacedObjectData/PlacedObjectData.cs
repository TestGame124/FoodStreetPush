using BayatGames.SaveGameFree.Types;

[System.Serializable]
public struct PlacedObjectData /*: BaseData */{

    public int Id;
    //public FixedString64Bytes Name;

    public ObjectType objectType;
    public Vector3Save position;
    public PlacedObjectTypeSO.Dir direction;
    public bool isRemoved;

    public bool Equals(PlacedObjectData other)
    {
        return Id == other.Id
            && position.x == other.position.x
            && position.y == other.position.y
            && position.z == other.position.z
            && objectType == other.objectType
            && direction == other.direction
            && isRemoved == other.isRemoved;
    }
    //public PlacedObjectData() : base() {}
}