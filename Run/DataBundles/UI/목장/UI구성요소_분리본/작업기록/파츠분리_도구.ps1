param([string]$Root = 'C:\Project_Run\Run\DataBundles\UI\목장\UI구성요소_분리본')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies @('System.Drawing.Common','System.Drawing.Primitives','System.Runtime','System.Collections','System.Runtime.InteropServices','System.ComponentModel.Primitives','System.Private.Windows.GdiPlus','System.Private.Windows.Core') -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class RanchParts {
  public static byte[] Read(Bitmap b) {
    var d=b.LockBits(new Rectangle(0,0,b.Width,b.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
    byte[] v=new byte[b.Width*b.Height*4];
    for(int y=0;y<b.Height;y++) Marshal.Copy(IntPtr.Add(d.Scan0,y*d.Stride),v,y*b.Width*4,b.Width*4);
    b.UnlockBits(d); return v;
  }
  public static Bitmap From(byte[] v,int w,int h) {
    var b=new Bitmap(w,h,PixelFormat.Format32bppArgb);
    var d=b.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
    for(int y=0;y<h;y++) Marshal.Copy(v,y*w*4,IntPtr.Add(d.Scan0,y*d.Stride),w*4);
    b.UnlockBits(d); return b;
  }
  public static Rectangle Bounds(Bitmap b) {
    var p=Read(b); int l=b.Width,t=b.Height,r=-1,bt=-1;
    for(int y=0;y<b.Height;y++) for(int x=0;x<b.Width;x++) if(p[(y*b.Width+x)*4+3]>12){l=Math.Min(l,x);t=Math.Min(t,y);r=Math.Max(r,x);bt=Math.Max(bt,y);}
    return r<l?new Rectangle(0,0,1,1):new Rectangle(l,t,r-l+1,bt-t+1);
  }
  public static Bitmap Crop(Bitmap b,int x,int y,int w,int h) {
    return b.Clone(new Rectangle(x,y,w,h),PixelFormat.Format32bppArgb);
  }
  public static Bitmap Trim(Bitmap b,int padding) {
    var box=Bounds(b);var p=Read(b);var v=new byte[(box.Width+padding*2)*(box.Height+padding*2)*4];
    for(int y=0;y<box.Height;y++) for(int x=0;x<box.Width;x++) {
      int s=((box.Y+y)*b.Width+box.X+x)*4, d=((y+padding)*(box.Width+padding*2)+x+padding)*4;
      if(p[s+3]>2) Array.Copy(p,s,v,d,4);
    }
    return From(v,box.Width+padding*2,box.Height+padding*2);
  }
  public static Bitmap Resize(Bitmap b,int w,int h) {
    var o=new Bitmap(w,h,PixelFormat.Format32bppArgb);
    using(var g=Graphics.FromImage(o)) {g.CompositingMode=CompositingMode.SourceCopy;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;using(var ia=new ImageAttributes()){ia.SetWrapMode(WrapMode.TileFlipXY);g.DrawImage(b,new Rectangle(0,0,w,h),0,0,b.Width,b.Height,GraphicsUnit.Pixel,ia);}}
    return o;
  }
  public static bool[] Flood(Bitmap b,int sx,int sy,int type) {
    int w=b.Width,h=b.Height; var p=Read(b);var m=new bool[w*h];var seen=new bool[w*h];var q=new Queue<int>(); q.Enqueue(sy*w+sx);
    while(q.Count>0){int i=q.Dequeue();if(seen[i])continue;seen[i]=true;int z=i*4;byte r=p[z+2],g=p[z+1],bl=p[z];
      bool ok=p[z+3]>100 && (type==0 ? r>210&&g>170&&bl>110 : r>45&&r<180&&g<118&&bl<90);
      if(!ok)continue;m[i]=true;int x=i%w,y=i/w;if(x>0)q.Enqueue(i-1);if(x<w-1)q.Enqueue(i+1);if(y>0)q.Enqueue(i-w);if(y<h-1)q.Enqueue(i+w);
    }return m;
  }
  public static bool[] Shape(Bitmap b,int[] xy,bool ellipse) {
    bool[] m=new bool[b.Width*b.Height];using(var path=new GraphicsPath()){
      if(ellipse)path.AddEllipse(xy[0],xy[1],xy[2],xy[3]);else{Point[] p=new Point[xy.Length/2];for(int i=0;i<p.Length;i++)p[i]=new Point(xy[i*2],xy[i*2+1]);path.AddPolygon(p);}
      for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++)m[y*b.Width+x]=path.IsVisible(x+.5f,y+.5f);
    }return m;
  }
  public static bool[] Union(bool[] a,bool[] b){for(int i=0;i<a.Length;i++)a[i]|=b[i];return a;}
  public static bool[] Inset(Bitmap b,int radius) {
    int w=b.Width,h=b.Height;var p=Read(b);var d=new int[w*h];var m=new bool[w*h];
    for(int y=0;y<h;y++)for(int x=0;x<w;x++){int i=y*w+x;d[i]=p[i*4+3]<180?0:Math.Min(x==0?0:d[i-1],y==0?0:d[i-w])+1;}
    for(int y=h-1;y>=0;y--)for(int x=w-1;x>=0;x--){int i=y*w+x;d[i]=Math.Min(d[i],Math.Min(x==w-1?0:d[i+1],y==h-1?0:d[i+w])+1);m[i]=d[i]>radius;}
    return m;
  }
  public static Bitmap[] Split(Bitmap b,bool[] inside) {
    int w=b.Width,h=b.Height;var p=Read(b);var bg=new byte[p.Length];var fg=new byte[p.Length];
    for(int y=0;y<h;y++)for(int x=0;x<w;x++){int i=y*w+x,z=i*4;if(p[z+3]<=2)continue;
      if(!inside[i])Array.Copy(p,z,fg,z,4);
      bool expanded=false;for(int dy=-1;dy<=1&&!expanded;dy++)for(int dx=-1;dx<=1;dx++){int nx=x+dx,ny=y+dy;if(nx>=0&&nx<w&&ny>=0&&ny<h&&inside[ny*w+nx]){expanded=true;break;}}
      if(expanded)Array.Copy(p,z,bg,z,4);
    }return new Bitmap[]{From(bg,w,h),From(fg,w,h)};
  }
  public static long[] Stats(Bitmap b) {var p=Read(b);long z=0,o=0,m=0;for(int i=3;i<p.Length;i+=4){if(p[i]==0)z++;else if(p[i]==255)o++;else m++;}return new long[]{b.Width,b.Height,z,o,m};}
}
'@
$layoutDir = Join-Path $Root '레이아웃'
$iconDir = Join-Path $Root '아이콘'
$recordDir = Join-Path $Root '작업기록'
$manifest = Get-Content -LiteralPath (Join-Path $recordDir '생성목록.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$parts = [System.Collections.Generic.List[object]]::new()
foreach ($item in $manifest.Icons) {
  $src=[System.Drawing.Bitmap]::FromFile((Join-Path $recordDir ('생성원본_'+$item.id+'.png')))
  $out=[RanchParts]::Trim($src,16)
  $out.Save((Join-Path $iconDir $item.file),[System.Drawing.Imaging.ImageFormat]::Png)
  $parts.Add([pscustomobject]@{Name=$item.id;Category='아이콘';File=('아이콘/'+$item.file);Width=$out.Width;Height=$out.Height})
  $out.Dispose();$src.Dispose()
}
function Save-Split([System.Drawing.Bitmap]$Picture,[bool[]]$Mask,[string]$Name) {
  $layers=[RanchParts]::Split($Picture,$Mask)
  $layers[0].Save((Join-Path $layoutDir ($Name+'_배경.png')),[System.Drawing.Imaging.ImageFormat]::Png)
  $layers[1].Save((Join-Path $layoutDir ($Name+'_프레임.png')),[System.Drawing.Imaging.ImageFormat]::Png)
  $parts.Add([pscustomobject]@{Name=$Name;Category='레이아웃';Background=('레이아웃/'+$Name+'_배경.png');Frame=('레이아웃/'+$Name+'_프레임.png');Width=$Picture.Width;Height=$Picture.Height})
  $layers[0].Dispose();$layers[1].Dispose()
}
$layout=[System.Drawing.Bitmap]::FromFile((Join-Path $recordDir '생성원본_레이아웃.png'))
$nameplate=[RanchParts]::Crop($layout,10,8,430,85)
$cream=[RanchParts]::Shape($nameplate,[int[]]@(34,20,265,20,274,24,276,34,282,42,276,50,276,59,267,66,34,66,27,61,27,52,22,45,27,35,27,26),$false)
$level=[RanchParts]::Shape($nameplate,[int[]]@(294,20,403,20,411,28,411,59,403,67,290,67,294,52,288,42,294,33),$false)
Save-Split $nameplate ([RanchParts]::Union($cream,$level)) '상단_목장정보'
$nameplate.Dispose()
$currency=[RanchParts]::Crop($layout,1064,10,232,59)
$currencyMain=[RanchParts]::Shape($currency,[int[]]@(22,14,178,14,178,43,22,43,15,38,15,20),$false)
$currencyAdd=[RanchParts]::Shape($currency,[int[]]@(190,15,211,15,216,20,216,40,211,45,190,45,185,40,185,20),$false)
$mask=[RanchParts]::Union($currencyMain,$currencyAdd)
Save-Split $currency $mask '상단_재화바'
$currency.Dispose()
$status=[RanchParts]::Crop($layout,968,191,154,81)
Save-Split $status ([RanchParts]::Shape($status,[int[]]@(20,14,133,14,140,20,140,47,134,53,84,53,77,63,69,53,20,53,13,47,13,20),$false)) '상태_말풍선'
$status.Dispose()
$expand=[RanchParts]::Crop($layout,1492,231,108,55)
Save-Split $expand ([RanchParts]::Shape($expand,[int[]]@(18,14,92,14,96,18,96,38,92,41,18,41,13,37,13,18),$false)) '확장_표지판'
$expand.Dispose()
$round=[RanchParts]::Crop($layout,1586,2,77,79)
Save-Split $round ([RanchParts]::Shape($round,[int[]]@(13,12,52,52),$true)) '상단_원형버튼'
$round.Dispose()
$menuSource=[System.Drawing.Bitmap]::FromFile((Join-Path $recordDir '생성원본_메뉴버튼.png'))
$menu=[RanchParts]::Trim($menuSource,12)
Save-Split $menu ([RanchParts]::Flood($menu,[int]($menu.Width/2),[int]($menu.Height/2),0)) '하단_공통메뉴버튼'
$menu.Dispose();$menuSource.Dispose()
$traySource=[System.Drawing.Bitmap]::FromFile((Join-Path $recordDir '생성원본_하단받침.png'))
$tray=[RanchParts]::Trim($traySource,12)
Save-Split $tray ([RanchParts]::Inset($tray,18)) '하단_나무받침판'
$tray.Dispose();$traySource.Dispose()
$layout.Dispose()
$parts | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $Root '파츠목록.json') -Encoding UTF8
$checks=Get-ChildItem -LiteralPath $layoutDir,$iconDir -Filter '*.png' | ForEach-Object {
  $b=[System.Drawing.Bitmap]::FromFile($_.FullName);$s=[RanchParts]::Stats($b);$b.Dispose()
  [pscustomobject]@{File=$_.Name;Width=$s[0];Height=$s[1];TransparentPixels=$s[2];OpaquePixels=$s[3];AntialiasedPixels=$s[4]}
}
$checks | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $recordDir '투명도_검사.json') -Encoding UTF8
$checks | Format-Table -AutoSize

# PNG 조각을 같은 좌표에 겹치는 검수용 조립. 새 그림은 생성하지 않는다.
$backgroundLayer=[System.Drawing.Bitmap]::new(1672,941,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$frameLayer=[System.Drawing.Bitmap]::new(1672,941,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$iconLayer=[System.Drawing.Bitmap]::new(1672,941,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$bgGraphics=[System.Drawing.Graphics]::FromImage($backgroundLayer)
$frGraphics=[System.Drawing.Graphics]::FromImage($frameLayer)
$icGraphics=[System.Drawing.Graphics]::FromImage($iconLayer)
foreach($g in @($bgGraphics,$frGraphics,$icGraphics)){$g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic;$g.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality}
$placements=[System.Collections.Generic.List[object]]::new()
function Draw-Sprite($Graphics,[string]$Path,[int]$X,[int]$Y,[int]$W,[int]$H,[bool]$KeepAspect=$false) {
  $p=[System.Drawing.Bitmap]::FromFile($Path)
  if($KeepAspect){$scale=[Math]::Min($W/$p.Width,$H/$p.Height);$dw=[int]($p.Width*$scale);$dh=[int]($p.Height*$scale);$X+=[int](($W-$dw)/2);$Y+=[int](($H-$dh)/2);$W=$dw;$H=$dh}
  $Graphics.DrawImage($p,[System.Drawing.Rectangle]::new($X,$Y,$W,$H));$p.Dispose()
}
function Draw-Part([string]$Name,[int]$X,[int]$Y,[int]$W,[int]$H) {
  Draw-Sprite $bgGraphics (Join-Path $layoutDir ($Name+'_배경.png')) $X $Y $W $H
  Draw-Sprite $frGraphics (Join-Path $layoutDir ($Name+'_프레임.png')) $X $Y $W $H
  $placements.Add([pscustomobject]@{Type='레이아웃';Name=$Name;X=$X;Y=$Y;Width=$W;Height=$H})
}
function Draw-Icon([string]$Name,[int]$X,[int]$Y,[int]$W,[int]$H) {
  $item=$manifest.Icons | Where-Object id -eq $Name
  Draw-Sprite $icGraphics (Join-Path $iconDir $item.file) $X $Y $W $H $true
  $placements.Add([pscustomobject]@{Type='아이콘';Name=$Name;X=$X;Y=$Y;Width=$W;Height=$H})
}
Draw-Part '하단_나무받침판' 58 838 1557 103
Draw-Part '상단_목장정보' 10 8 430 85
Draw-Part '상단_재화바' 1064 10 232 59
Draw-Part '상단_재화바' 1283 10 213 59
Draw-Part '상단_원형버튼' 1586 2 77 79
Draw-Part '상태_말풍선' 968 191 154 81
Draw-Part '상태_말풍선' 648 321 154 81
Draw-Part '확장_표지판' 1492 231 108 55
Draw-Part '확장_표지판' 73 637 108 55
$menuXs=@(170,430,690,950,1210)
$menuIcons=@('목장','몬스터','건설','훈련','경기장')
for($i=0;$i -lt 5;$i++){Draw-Part '하단_공통메뉴버튼' $menuXs[$i] 835 252 93;Draw-Icon $menuIcons[$i] ($menuXs[$i]+19) 819 103 103}
Draw-Icon '목장' 22 5 90 85
Draw-Icon '골드' 1065 10 54 54
Draw-Icon '보석' 1295 11 57 54
Draw-Icon '추가' 1253 27 25 25
Draw-Icon '추가' 1458 27 25 25
Draw-Icon '수첩' 1510 7 69 68
Draw-Icon '알림' 1555 8 21 21
Draw-Icon '설정' 1603 16 45 46
Draw-Icon '완료' 982 208 33 33
Draw-Icon '망치' 660 337 39 40
Draw-Icon '잠금' 1522 187 44 51
Draw-Icon '잠금' 104 593 44 51
$bgGraphics.Dispose();$frGraphics.Dispose();$icGraphics.Dispose()
$lay=[System.Drawing.Bitmap]::new(1672,941,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$lg=[System.Drawing.Graphics]::FromImage($lay);$lg.DrawImageUnscaled($backgroundLayer,0,0);$lg.DrawImageUnscaled($frameLayer,0,0);$lg.Dispose()
$small=[RanchParts]::Resize($lay,1280,720);$small.Save((Join-Path $layoutDir '전체배치_글자와아이콘없음_1280x720.png'));$small.Dispose()
$ui=[System.Drawing.Bitmap]::new(1672,941,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$ug=[System.Drawing.Graphics]::FromImage($ui);$ug.DrawImageUnscaled($lay,0,0);$ug.DrawImageUnscaled($iconLayer,0,0);$ug.Dispose()
$small=[RanchParts]::Resize($ui,1280,720);$small.Save((Join-Path $Root 'UI_전체_글자없음_1280x720.png'));$small.Dispose()
$preview=[System.Drawing.Bitmap]::new(1672,941)
$pg=[System.Drawing.Graphics]::FromImage($preview)
for($y=0;$y -lt 941;$y+=24){for($x=0;$x -lt 1672;$x+=24){$c=if((($x/24+$y/24)%2)-eq 0){[System.Drawing.Color]::FromArgb(213,216,213)}else{[System.Drawing.Color]::FromArgb(225,227,224)};$brush=[System.Drawing.SolidBrush]::new($c);$pg.FillRectangle($brush,$x,$y,24,24);$brush.Dispose()}}
$pg.DrawImageUnscaled($ui,0,0)
$pg.TextRenderingHint=[System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
function Write-Label([string]$Text,[int]$X,[int]$Y,[int]$Size,[System.Drawing.Color]$Color) {
  $font=[System.Drawing.Font]::new('맑은 고딕',$Size,[System.Drawing.FontStyle]::Bold,[System.Drawing.GraphicsUnit]::Pixel)
  $brush=[System.Drawing.SolidBrush]::new($Color);$pg.DrawString($Text,$font,$brush,$X,$Y);$brush.Dispose();$font.Dispose()
}
$brown=[System.Drawing.Color]::FromArgb(77,37,21)
Write-Label '나의 목장' 123 25 35 $brown
Write-Label 'Lv. 8' 313 31 29 ([System.Drawing.Color]::FromArgb(255,239,207))
Write-Label '12,500' 1140 24 27 $brown
Write-Label '120' 1372 24 27 $brown
Write-Label '훈련 완료' 1019 211 21 $brown
Write-Label '건설 중' 703 341 22 $brown
Write-Label '확장' 1522 246 23 $brown
Write-Label '확장' 102 653 23 $brown
$labels=@('목장','몬스터','건설','훈련','경기장')
for($i=0;$i -lt 5;$i++){Write-Label $labels[$i] ($menuXs[$i]+128) 864 29 $brown}
$pg.Dispose();$small=[RanchParts]::Resize($preview,1280,720);$small.Save((Join-Path $Root '00_UI조립_미리보기.png'));$small.Dispose();$preview.Dispose()
$backgroundLayer.Dispose();$frameLayer.Dispose();$iconLayer.Dispose();$lay.Dispose();$ui.Dispose()
[pscustomobject]@{CoordinateOrigin='top-left';ReferenceWidth=1672;ReferenceHeight=941;PreviewWidth=1280;PreviewHeight=720;Note='1280 기준 좌표는 X*1280/1672, Y*720/941. PNG 프레임과 배경은 같은 크기·위치로 겹침.';Elements=$placements} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $Root '배치좌표.json') -Encoding UTF8

# 모든 결과의 실제 PNG를 밝은 배경과 어두운 배경에 표시한다.
$sheet=[System.Drawing.Bitmap]::new(1600,1510)
$sg=[System.Drawing.Graphics]::FromImage($sheet)
$sg.Clear([System.Drawing.Color]::FromArgb(241,238,230))
$sg.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$sg.TextRenderingHint=[System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$titleFont=[System.Drawing.Font]::new('맑은 고딕',34,[System.Drawing.FontStyle]::Bold,[System.Drawing.GraphicsUnit]::Pixel)
$bodyFont=[System.Drawing.Font]::new('맑은 고딕',20,[System.Drawing.FontStyle]::Regular,[System.Drawing.GraphicsUnit]::Pixel)
$smallFont=[System.Drawing.Font]::new('맑은 고딕',17,[System.Drawing.FontStyle]::Regular,[System.Drawing.GraphicsUnit]::Pixel)
$textBrush=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(70,44,29))
$light=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255,253,247))
$dark=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(53,60,57))
$sg.DrawString('목장 UI · 레이아웃 / 아이콘 분리',$titleFont,$textBrush,38,22)
$sg.DrawString('아이콘 14종 · 레이아웃 7종의 배경과 프레임 · 투명 PNG',$bodyFont,$textBrush,40,71)
for($i=0;$i -lt $manifest.Icons.Count;$i++){
  $col=$i%7;$row=[int][Math]::Floor($i/7);$x=35+$col*220;$y=119+$row*218
  $sg.FillRectangle($(if($row-eq 0){$light}else{$dark}),$x,$y,210,172)
  Draw-Sprite $sg (Join-Path $iconDir $manifest.Icons[$i].file) ($x+20) ($y+8) 170 156 $true
  $sg.DrawString($manifest.Icons[$i].id,$bodyFont,$textBrush,$x+7,$y+179)
}
$sg.DrawString('레이아웃 파츠  |  왼쪽: 배경     가운데: 프레임     오른쪽: 조립',$bodyFont,$textBrush,40,566)
$layoutNames=@('상단_목장정보','상단_재화바','상단_원형버튼','상태_말풍선','확장_표지판','하단_공통메뉴버튼','하단_나무받침판')
for($i=0;$i -lt $layoutNames.Count;$i++){
  $y=612+$i*121;$name=$layoutNames[$i];$sg.DrawString($name.Replace('_',' '),$smallFont,$textBrush,42,$y+3)
  for($j=0;$j -lt 3;$j++){$x=275+$j*425;$sg.FillRectangle($dark,$x,$y,405,102)}
  $bgPath=Join-Path $layoutDir ($name+'_배경.png');$frPath=Join-Path $layoutDir ($name+'_프레임.png')
  Draw-Sprite $sg $bgPath 285 ($y+8) 385 86 $true
  Draw-Sprite $sg $frPath 710 ($y+8) 385 86 $true
  Draw-Sprite $sg $bgPath 1135 ($y+8) 385 86 $true
  Draw-Sprite $sg $frPath 1135 ($y+8) 385 86 $true
}
$sg.DrawString('원본 스타일을 참고해 재제작했습니다. 문구·수치는 조립 미리보기에만 표시합니다.',$smallFont,$textBrush,40,1470)
$sg.Dispose();$sheet.Save((Join-Path $Root '01_분리파츠_미리보기.png'));$sheet.Dispose()
foreach($d in @($titleFont,$bodyFont,$smallFont,$textBrush,$light,$dark)){$d.Dispose()}
