#ifndef PROJECT_RUN_RANCH_ROADS
#define PROJECT_RUN_RANCH_ROADS
TEXTURE2D(_RanchRoadMask); SAMPLER(sampler_RanchRoadMask);
float4 _RanchRoadGrid, _RanchRoadSize;
float RanchRoadCell(float2 cell)
{
    if(any(cell<0) || any(cell>=_RanchRoadSize.xy)) return 0;
    return SAMPLE_TEXTURE2D_LOD(_RanchRoadMask,sampler_RanchRoadMask,(cell+.5)/max(_RanchRoadSize.xy,1),0).r;
}
float RanchRoadDistance(float2 world)
{
    float size=_RanchRoadGrid.z;
    if(size<.01) return 100;
    float2 grid=(world-_RanchRoadGrid.xy)/size, cell=floor(grid), f=frac(grid)*size;
    if(RanchRoadCell(cell)>.5)
    {
        float edge=100;
        if(RanchRoadCell(cell+float2(-1,0))<.5)edge=min(edge,f.x);
        if(RanchRoadCell(cell+float2(1,0))<.5)edge=min(edge,size-f.x);
        if(RanchRoadCell(cell+float2(0,-1))<.5)edge=min(edge,f.y);
        if(RanchRoadCell(cell+float2(0,1))<.5)edge=min(edge,size-f.y);
        return -edge;
    }
    float nearest=size;
    [unroll] for(int y=-1;y<=1;y++)
    [unroll] for(int x=-1;x<=1;x++)
    {
        float2 neighbor=cell+float2(x,y);
        if(RanchRoadCell(neighbor)>.5)
        {
            float2 q=abs(grid-neighbor-.5)*size-size*.5;
            nearest=min(nearest,length(max(q,0)));
        }
    }
    return nearest;
}
#endif
