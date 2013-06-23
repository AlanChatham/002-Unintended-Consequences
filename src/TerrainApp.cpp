#include "cinder/app/AppBasic.h"
#include "cinder/gl/gl.h"
#include "cinder/gl/Texture.h"
#include "cinder/Text.h"
#include "cinder/gl/Fbo.h"
#include "cinder/gl/Vbo.h"
#include "cinder/gl/GlslProg.h"
#include "cinder/ImageIo.h"
#include "cinder/Utilities.h"
#include "cinder/Camera.h"
#include "cinder/Rand.h"
#include "cinder/Sphere.h"
#include "Resources.h"
#include "CubeMap.h"
#include "Room.h"
#include "HeadCam.h"
#include "Terrain.h"
#include "RDiffusion.h"
#include "Cinder-Freenect\src\CinderFreenect.h"
#include "OscListener.h"
#include "OscMessage.h"

using namespace ci;
using namespace ci::app;
using namespace std;

#define APP_WIDTH		600 //1024
#define APP_HEIGHT		400//512 //720
#define ROOM_FBO_RES	2
#define FBO_SIZE		512 // This is the size of the frame buffer. The reaction diffusion gets set by this somehow, and gets mapped onto the terrain at a fbo_size/app_width ratio
#define VBO_SIZE		600 //600 // This is the size of the vertex mesh. It should be the same size as the room
#define ROOM_HEIGHT		400.0f //Y dimension
#define ROOM_WIDTH		600.0f	//X dimension
#define ROOM_DEPTH		600.0f	//Z dimension

class TerrainApp : public AppBasic {
  public:
	virtual void	prepareSettings( Settings *settings );
	virtual void	setup();
	virtual void	mouseDown( MouseEvent event );
	virtual void	mouseUp( MouseEvent event );
	virtual void	mouseMove( MouseEvent event );
	virtual void	mouseDrag( MouseEvent event );
	virtual void	mouseWheel( MouseEvent event );
	virtual void	keyDown( KeyEvent event );
	virtual void	update();
	void			drawIntoRoomFbo();
	virtual void	draw();
	void			drawGuts(Area);
	void			drawSphere();
	void			drawRandomSpheres();
	void			drawNodes();
	void			drawTerrain();
	void			drawInfoPanel();
	void			createNewWindow();
	void			setCameras(const osc::Message*);
	void 			adjustProjection(Vec3f bottomLeft, Vec3f bottomRight, Vec3f topLeft, Vec3f eyePos, float n, float f);

	


	// OSC listener
	osc::Listener   oscListener;
	
	// SHADERS
	gl::GlslProg		mRoomShader;
	gl::GlslProg		mSphereShader;
	
	// TEXTURES
	gl::Texture			mIconTex;
	CubeMap				mCubeMap;
	
	// ROOM
	Room				mRoom;
	gl::Fbo				mRoomFbo;
	gl::Texture			mRoomBackWallTex;
	gl::Texture			mRoomLeftWallTex;
	gl::Texture			mRoomRightWallTex;
	gl::Texture			mRoomCeilingTex;
	gl::Texture			mRoomFloorTex;
	gl::Texture			mRoomBlankTex;
	
	// ENVIRONMENT AND LIGHTING
	Color				mFogColor;
	Color				mBlueSkyColor;
	Vec3f				mLightPosDay;
	Vec3f				mLightPosNight;
	Vec3f				mLightPos, mLightDir;
	float				mLightDist;
	
	// TERRAIN
	Terrain				mTerrain;
	Vec3f				mTerrainScale;
	Color				mSandColor;
	gl::Texture			mGradientTex;
	gl::Texture			mSandNormalTex;
	float				mZoomMulti, mZoomMultiDest;
	
	// REACTION DIFFUSION
	RDiffusion			mRd;
	gl::GlslProg		mRdShader, mHeightsShader, mNormalsShader, mTerrainShader;
	gl::Texture			mGlowTex;

	// SPHERE
	Sphere				mSphere;
	Vec3f				mSpherePos, mSpherePosDest;

	std::vector<Sphere>	mRandomSpheres;
	
	// MOUSE
	Vec2f				mMouseRightPos;
	Vec2f				mMousePos, mMousePosNorm, mMouseDownPos, mMouseOffset;
	bool				mMouseLeftDown, mMouseRightDown;

	// FBO viewports
	gl::Fbo				mFbo0, mFbo1;
	Area				mViewArea0, mViewArea1;
};


	//Fuck it, making these globals since I can't figure out how to
	// create a callback to a member function that actually works.
	HeadCam			mHeadCam0;
	HeadCam			mActiveHeadCam;
	HeadCam			mHeadCam1;

void TerrainApp::prepareSettings( Settings *settings )
{
	settings->setWindowSize( APP_WIDTH, APP_HEIGHT );
	//settings->setBorderless( true );
	settings->setBorderless( false );
}

void TerrainApp::setCameras(const osc::Message * message){
	// Sanity check that we have our legit head message
	if (message->getAddress() == "/head" && message->getNumArgs() == 3){
		//console() << "New message received" << std::endl;
		float headX = message->getArgAsFloat(0);
		float headY = message->getArgAsFloat(1);
		float headZ = message->getArgAsFloat(2);

		headX = ((3 * (headX )) / 5) - 600;
		headY = (3 * headY ) / 5 + 150;
		headZ = (headZ * 3.28f * 50); // Seems like we're doing 100px per foot

		float screenKinectDistance = 450;
		headZ = screenKinectDistance + headZ;

/*		float side2;
		float side3;

		// Get the size of the triangle made up by the camera and the screen - FOV is the vertical angle
		float side1 = ROOM_HEIGHT; // The height of the screen
		if (headX < (-ROOM_HEIGHT / 2)){
			side2 = sqrtf(powf(headZ, 2) + powf(headY - (ROOM_HEIGHT / 2), 2) + powf(headX + (ROOM_WIDTH / 2), 2)); 
			side3 = sqrtf(powf(headZ, 2) + powf(headY + (ROOM_HEIGHT / 2), 2) + powf(headX + (ROOM_WIDTH / 2), 2)); 
		}
		else if (headX > (ROOM_HEIGHT / 2)){
			side2 = sqrtf(powf(headZ, 2) + powf(headY - (ROOM_HEIGHT / 2), 2) + powf(headX - (ROOM_WIDTH / 2), 2));
			side3 = sqrtf(powf(headZ, 2) + powf(headY + (ROOM_HEIGHT / 2), 2) + powf(headX - (ROOM_WIDTH / 2), 2)); 
		}
		else{
			side2 = sqrtf(powf(headZ, 2) + powf(headY - (ROOM_HEIGHT / 2), 2));
			side3 = sqrtf(powf(headZ, 2) + powf(headY + (ROOM_HEIGHT / 2), 2)); 
		}
		console() << "side 2 = " << side2 << std::endl;
		console() << "side 3 = " << side3 << std::endl;

		// Law of cosines in 2 easy parts!
		float fovAngle = (powf(side2, 2) + powf(side3, 2) - powf(side1, 2)) / (2 * side2 * side3);
		fovAngle = acos(fovAngle) * (180 / 3.14159265f);
		console() << "FOVAngle = " <<fovAngle << std::endl;
*/		
//		mHeadCam0.setEye(Vec3f(headX, headY, mHeadCam0.mCamDist));
		mHeadCam0.setEye(Vec3f(headX, headY, headZ + (ROOM_DEPTH / 2))); // The 300 is b/c the coordinates are based on room center
//		mHeadCam0.setCenter(Vec3f(mHeadCam0.mCenter.x, mHeadCam0.mCenter.y, 0.0f));
		//mHeadCam0.setCenter(Vec3f(-headX / 10, mHeadCam0.mCenter.y, 0.0f));
//		mHeadCam0.setFov(fovAngle);
//			mHeadCam1.mEye = Vec3f(headX, headY, mHeadCam0.mCamDist);
//		console() << mHeadCam0.mEye << std::endl;
			
	}
}



void TerrainApp::setup()
{	
	setFrameRate(30);

	mFbo0 = gl::Fbo(APP_WIDTH / 2, APP_HEIGHT / 2);
	mFbo1 = gl::Fbo(APP_WIDTH / 2, APP_HEIGHT / 2);
	
	// Setup the camera for the main window
	mHeadCam0 = HeadCam( 1200.0f, getWindowAspectRatio() );
	mHeadCam0.mEye.y = 0;
	mHeadCam0.mCenter = Vec3f(0,0, ROOM_DEPTH / 2 );
	console() << "Eye height = " << mHeadCam0.mEye.y << std::endl;

	mHeadCam1 = HeadCam( 1200.0f, getWindowAspectRatio() );
	mHeadCam1.mEye.y = 0;
	mHeadCam1.mCenter = Vec3f(0,0, ROOM_DEPTH / 2 );
	//getWindow()->setUserData( new HeadCam( -500.0f, getWindowAspectRatio() ));
	getWindow()->setUserData(&mHeadCam1);
	// And create a new window with stuff too
//	createNewWindow();

	// Set up a listener for OSC messages
	oscListener.setup(7110);

	// LOAD SHADERS
	try {
		mRoomShader		= gl::GlslProg( loadResource( ROOM_VERT_ID ), loadResource( ROOM_FRAG_ID ) );
		mRdShader		= gl::GlslProg( loadResource( PASS_THRU_VERT_ID ), loadResource( RD_FRAG_ID ) );
		mHeightsShader	= gl::GlslProg( loadResource( PASS_THRU_VERT_ID ), loadResource( HEIGHTS_FRAG_ID ) );
		mNormalsShader	= gl::GlslProg( loadResource( PASS_THRU_VERT_ID ), loadResource( NORMALS_FRAG_ID ) );
		mTerrainShader	= gl::GlslProg( loadResource( TERRAIN_VERT_ID ), loadResource( TERRAIN_FRAG_ID ) );
		mSphereShader	= gl::GlslProg( loadResource( SPHERE_VERT_ID ), loadResource( SPHERE_FRAG_ID ) );
	} catch( gl::GlslProgCompileExc e ) {
		std::cout << e.what() << std::endl;
		quit();
	}
	
	// TEXTURE FORMAT
	gl::Texture::Format mipFmt;
    mipFmt.enableMipmapping( true );
    mipFmt.setMinFilter( GL_LINEAR_MIPMAP_LINEAR );    
    mipFmt.setMagFilter( GL_LINEAR );
	
	// LOAD TEXTURES
	mIconTex		= gl::Texture( loadImage( loadResource( ICON_TERRAIN_ID ) ) );
	mGlowTex		= gl::Texture( loadImage( loadResource( GLOW_ID ) ) );
	mCubeMap		= CubeMap( GLsizei(512), GLsizei(512),
							   Surface8u( loadImage( loadResource( RES_CUBE1_ID ) ) ),
							   Surface8u( loadImage( loadResource( RES_CUBE2_ID ) ) ),
							   Surface8u( loadImage( loadResource( RES_CUBE3_ID ) ) ),
							   Surface8u( loadImage( loadResource( RES_CUBE4_ID ) ) ),
							   Surface8u( loadImage( loadResource( RES_CUBE5_ID ) ) ),
							   Surface8u( loadImage( loadResource( RES_CUBE6_ID ) ) )
							   );
	
	// ROOM
	gl::Fbo::Format roomFormat;
	roomFormat.setColorInternalFormat( GL_RGB );
	mRoomFbo			= gl::Fbo( APP_WIDTH/ROOM_FBO_RES, APP_HEIGHT/ROOM_FBO_RES, roomFormat );
	bool isPowerOn		= false;
	bool isGravityOn	= true;
	// Build us a room of a certain size
	mRoom				= Room( Vec3f( ROOM_WIDTH / 2, ROOM_HEIGHT / 2, ROOM_DEPTH / 2 ), isPowerOn, isGravityOn );	
	//mRoom				= Room( Vec3f( 800.0f, 200.0f, 800.0f ), isPowerOn, isGravityOn );	
	mRoomBackWallTex	= gl::Texture( loadImage( loadResource( BACK_WALL_TEX_ID ) ) );
	mRoomLeftWallTex	= gl::Texture( loadImage( loadResource( WALL_TEX_ID ) ) );
	mRoomRightWallTex	= gl::Texture( loadImage( loadResource( WALL_TEX_ID ) ) );
	mRoomCeilingTex		= gl::Texture( loadImage( loadResource( CEILING_TEX_ID ) ) );
	mRoomFloorTex		= gl::Texture( loadImage( loadResource( FLOOR_TEX_ID ) ) );
	mRoomBlankTex		= gl::Texture( loadImage( loadResource( BLANK_TEX_ID ) ) );
	
	// ENVIRONMENT AND LIGHTING
	mFogColor		= Color( 255.0f/255.0f, 255.0f/255.0f, 230.0f/255.0f );
	//mFogColor		= Color( 0.0f/255.0f, 0.0f/255.0f, 230.0f/255.0f );
	//mSandColor		= Color( 255.0f/255.0f, 133.0f/255.0f,  67.0f/255.0f );
	mSandColor		= Color( 255.0f/255.0f, 0.0f,  0.0f );
	mBlueSkyColor	= Color( 1.0f/255.0f, 78.0f/255.0f, 174.0f/255.0f );
	mLightPos		= Vec3f( 0.0f, 0.6f, 1.0f );
	mLightDist		= 500.0f;
	mLightDir		= mLightPos.normalized();
	
	// TERRAIN
	mTerrainScale	= Vec3f( 1.0f, 2.0f, 1.0f );
	//mTerrainScale	= Vec3f( 2.0f, 5.0f, 1.0f );
	mTerrain		= Terrain( VBO_SIZE, VBO_SIZE );
	mZoomMulti		= 1.0f;
	mZoomMultiDest	= 1.0f;
	mGradientTex	= gl::Texture( loadImage( loadResource( GRADIENT_TEX_ID) ) );
	mSandNormalTex	= gl::Texture( loadImage( loadResource( SAND_NORMAL_TEX_ID ) ) );
	mSandNormalTex.setWrap( GL_REPEAT, GL_REPEAT );
	
	// REACTION DIFFUSION
	// This gets placed over the mesh I guess?
	mRd				= RDiffusion( APP_WIDTH, APP_HEIGHT); //FBO_SIZE, FBO_SIZE );

	// SPHERE
	mSpherePos		= Vec3f::zero();
	mSpherePosDest	= Vec3f::zero();
	mSphere.setCenter( mSpherePosDest );
	mSphere.setRadius( 20.0f );

	for (int i = 0; i < 3; i++){
		for (int j = 0; j < 3; j++){
			for (int k = 0; k < 3; k++){
				Sphere newSphere;
				newSphere.setCenter(Vec3f(i * (ROOM_WIDTH / 2) - (ROOM_WIDTH/2), j * (ROOM_HEIGHT / 2), k * (ROOM_DEPTH / 2) - (ROOM_DEPTH/2)));
				newSphere.setRadius(30.0f);
				mRandomSpheres.push_back(newSphere);
			}
		}
	}
	
	// MOUSE
	mMouseRightPos	= getWindowCenter();
	mMousePos		= Vec2f::zero();
	mMouseDownPos	= Vec2f::zero();
	mMouseOffset	= Vec2f::zero();
	mMousePosNorm	= Vec2f::zero();
	mMouseLeftDown	= false;
	mMouseRightDown	= false;
	
	mRoom.init();
}

void TerrainApp::mouseDown( MouseEvent event )
{
	mMouseDownPos = event.getPos();
	mMouseOffset = Vec2f::zero();
	
	if( event.isLeft() ){
		mMouseLeftDown = true;
	}
	
	if( event.isRight() ){
		mMouseRightDown = true;
		mMouseRightPos	= getWindowSize() - event.getPos();
	}
}

void TerrainApp::mouseUp( MouseEvent event )
{
	if( event.isLeft() ){
		mMouseLeftDown = false;
	}
	
	if( event.isRight() ){
		mMouseRightDown = false;
		mMouseRightPos	= getWindowSize() - event.getPos();
	}
	
	mMouseOffset = Vec2f::zero();
}

void TerrainApp::mouseMove( MouseEvent event )
{
	mMousePos = event.getPos();
	if( event.isRight() )
		mMouseRightPos	= getWindowSize() - event.getPos();
}

void TerrainApp::mouseDrag( MouseEvent event )
{
	mouseMove( event );
	mMouseOffset = ( mMousePos - mMouseDownPos ) * 0.7f;
}

void TerrainApp::mouseWheel( MouseEvent event )
{
	float dWheel	= event.getWheelIncrement();
	
	if( event.isShiftDown() ){
		mZoomMultiDest += dWheel * 0.001f;
		mZoomMultiDest = constrain( mZoomMultiDest, 0.1f, 1.0f );
	} else {
		mRoom.adjustTimeMulti( dWheel );
	}
}

void TerrainApp::keyDown( KeyEvent event )
{
	switch ( event.getChar() ) {
		case ' ':	mRoom.togglePower();
					mHeadCam0.setPreset( 1 );	break;
		case 'f':	mRd.mParamF += 0.001f;		break;
		case 'F':	mRd.mParamF -= 0.001f;		break;
		case 'k':	mRd.mParamK += 0.001f;		break;
		case 'K':	mRd.mParamK -= 0.001f;		break;
		case 'n':	mRd.mParamN += 0.005f;		break;
		case 'N':	mRd.mParamN -= 0.005f;		break;
		case 'w':	mRd.mParamWind += 0.05f;	break;
		case 'W':	mRd.mParamWind -= 0.05f;	break;
		case '.':	mTerrainScale.x += 0.1f;	break;
		case ',':	mTerrainScale.x -= 0.1f;	break;
		case '/':	mRd.mXOffset -= 0.1f;	break;
		case '1':	mRd.setMode(1);				break;
		case '2':	mRd.setMode(2);				break;
		case '3':	mRd.setMode(3);				break;
		case 'c':	mHeadCam0.setPreset( 0 );	break;
		case 'C':	mHeadCam0.setPreset( 2 );	break;
		case 'r':   mHeadCam0.setEye(Vec3f(mHeadCam0.mEye.x, mHeadCam0.mEye.y, mHeadCam0.mEye.z));	break;
		default:								break;
	}
	
	switch( event.getCode() ){
		case KeyEvent::KEY_UP:		mMouseRightPos = Vec2f( 222.0f, 205.0f ) + getWindowCenter();	break;
		//case KeyEvent::KEY_LEFT:	mMouseRightPos = Vec2f(-128.0f,-178.0f ) + getWindowCenter();	break;
		case KeyEvent::KEY_LEFT:	mHeadCam0.setEye(Vec3f(mHeadCam0.mEye.x - 100, mHeadCam0.mEye.y, mHeadCam0.mEye.z));
									mHeadCam0.setCenter(Vec3f(mHeadCam0.mCenter.x, mHeadCam0.mCenter.y, mHeadCam0.mCenter.z));
									mHeadCam0.setFov(mHeadCam0.mFov);	break;
		//case KeyEvent::KEY_RIGHT:	mMouseRightPos = Vec2f(-256.0f, 122.0f ) + getWindowCenter();	break;
		case KeyEvent::KEY_RIGHT:	mHeadCam0.setEye(Vec3f(mHeadCam0.mEye.x + 100, mHeadCam0.mEye.y, mHeadCam0.mEye.z));	break;
		case KeyEvent::KEY_DOWN:	mMouseRightPos = Vec2f(   0.0f,   0.0f ) + getWindowCenter();	break;
		default: break;
	}
	
	std::cout << "F: " << mRd.mParamF << " K: " << mRd.mParamK << std::endl;
}



void TerrainApp::update()
{	
	float x = mMouseRightPos.x - getWindowSize().x * 0.5f;
	float y = mSphere.getCenter().y;
	float z = mMouseRightPos.y - getWindowSize().y * 0.5f;
	mSpherePosDest = Vec3f( x, y, z );
	mSpherePos -= ( mSpherePos - mSpherePosDest ) * 0.02f;
	
	mSphere.setCenter( mSpherePos );
	
	// ROOM
	mRoom.update();
	
	mZoomMulti		-= ( mZoomMulti - mZoomMultiDest ) * 0.1f;
	
	gl::disableDepthRead();
	gl::disableDepthWrite();
	gl::color( Color( 1, 1, 1 ) );
	gl::disableAlphaBlending();
	
	// REACTION DIFFUSION
	mRd.update( mRoom.getTimeDelta(), &mRdShader, mGlowTex, mMouseRightDown, mSphere.getCenter().xz(), mZoomMulti );
	mRd.drawIntoHeightsFbo( &mHeightsShader, mTerrainScale );
	mRd.drawIntoNormalsFbo( &mNormalsShader );
	
	
	// CAMERA
	HeadCam *thisViewsCam = getWindow()->getUserData<HeadCam>();
			// Get in OSC data
	osc::Message headMessage;

	while( oscListener.hasWaitingMessages() ) {
		osc::Message message;
		oscListener.getNextMessage( &message );
		
		setCameras(&message);
	}

	//if( mMouseLeftDown ) 
	//	mActiveHeadCam.dragCam( ( mMouseOffset ) * 0.01f, ( mMouseOffset ).length() * 0.01 );
	//mActiveHeadCam.update( mRoom.getPower(), 0.5f );
	mHeadCam0.update(mRoom.getPower(), 0.5f);
	mHeadCam1.update(mRoom.getPower(), 0.5f);
}

void TerrainApp::drawIntoRoomFbo()
{
	HeadCam *thisViewsCam = getWindow()->getUserData<HeadCam>();

	mRoomFbo.bindFramebuffer();
	gl::clear( ColorA( 0.0f, 0.0f, 0.0f, 0.0f ), true );
	
	gl::setMatricesWindow( mRoomFbo.getSize(), false );
	gl::setViewport( mRoomFbo.getBounds() );
	gl::disableAlphaBlending();
	gl::enable( GL_TEXTURE_2D );
	glEnable( GL_CULL_FACE );
	glCullFace( GL_BACK );
	Matrix44f m;
	m.setToIdentity();
	m.scale( mRoom.getDims() );

	mCubeMap.bind();
	mRoomShader.bind();
	mRoomShader.uniform( "cubeMap", 0 );
	mRoomShader.uniform( "mvpMatrix", mActiveHeadCam.mMvpMatrix );
	mRoomShader.uniform( "mMatrix", m );
	mRoomShader.uniform( "eyePos", mActiveHeadCam.mEye );
	mRoomShader.uniform( "roomDims", mRoom.getDims() );
	mRoomShader.uniform( "power", mRoom.getPower() );
	mRoomShader.uniform( "lightPower", mRoom.getLightPower() );
	mRoomShader.uniform( "timePer", mRoom.getTimePer() * 1.5f + 0.5f );
	mRoom.draw();
	mRoomShader.unbind();
	
	mRoomFbo.unbindFramebuffer();
	glDisable( GL_CULL_FACE );
}

int counter = 0;

void TerrainApp::draw(){
	/*	counter++;
	if (counter % 2 == 0){
		INPUT Input = {0};
		Input.type = INPUT_KEYBOARD;
		Input.ki.wVk = VK_LEFT;
		SendInput(1, &Input, sizeof(Input));
	}
	*/
	mViewArea0 = Area(0, 0, getWindowSize().x / 2,getWindowSize().y);
	mViewArea1 = Area(getWindowSize().x / 2, 0, getWindowSize().x, getWindowSize().y);

	gl::clear( ColorA( 0.1f, 0.1f, 0.1f, 0.0f ), true );

	mActiveHeadCam = mHeadCam0;
	drawGuts(mViewArea1);

	mActiveHeadCam = mHeadCam1;
	drawGuts(mViewArea0);
}

void TerrainApp::drawGuts(Area area)
{
	// Clear the screen
	
	float power = mRoom.getPower();
	// ROOM
	// This used to be in Update for some reason.
	// That made it not be able to get the correct rendering camera.
	drawIntoRoomFbo();
	
	gl::setMatricesWindow( getWindowSize(), false );
	// Set the viewport to match the whole thing
//	gl::setViewport( getWindowBounds() );
	gl::setViewport(area);
	

	gl::disableDepthRead();
	gl::disableDepthWrite();
	gl::enableAlphaBlending();
	gl::enable( GL_TEXTURE_2D );
	gl::color( ColorA( 1.0f, 1.0f, 1.0f, 1.0f ) );
	

	// DRAW ROOM FBO
	// Bind 
	mRoomFbo.bindTexture();
	gl::drawSolidRect( getWindowBounds() );
	HeadCam *thisViewsCam = getWindow()->getUserData<HeadCam>();
	gl::setMatrices( mActiveHeadCam.getCam() );

	gl::color( ColorA( power, power, power, power * 0.1f + 0.9f ) );
	

	// DRAW INFO PANEL
	drawInfoPanel();
	
	gl::enable( GL_TEXTURE_2D );
	gl::color( ColorA( 1.0f, 1.0f, 1.0f, 1.0f ) );
	
	// DRAW WALLS
	mRoom.drawWalls( mRoom.getPower(), mRoomBackWallTex, mRoomLeftWallTex, mRoomRightWallTex, mRoomCeilingTex, mRoomFloorTex, mRoomBlankTex );
	
	gl::enableAlphaBlending();
	gl::enableDepthRead();
	gl::enableDepthWrite();
	gl::enable( GL_TEXTURE_2D );
	
	// DRAW TERRAIN
	drawTerrain();
	
	gl::disable( GL_TEXTURE_2D );
	
	// DRAW SPHERE
	drawSphere();
}

void TerrainApp::drawSphere()
{
	HeadCam *thisViewsCam = getWindow()->getUserData<HeadCam>();
	Vec3f spherePos = mSphere.getCenter();
	Vec3f roomDims	= mRoom.getDims();
	float x = ( spherePos.x + roomDims.x ) / ( roomDims.x * 2.0f );
	float y = ( spherePos.z + roomDims.z ) / ( roomDims.z * 2.0f );

	Vec2f texCoord = Vec2f( x, y );
	
	mCubeMap.bind();
	mRd.getHeightsTexture().bind( 1 );
	mRd.getNormalsTexture().bind( 2 );
	mSphereShader.bind();
	mSphereShader.uniform( "cubeMap", 0 );
	mSphereShader.uniform( "heightsTex", 1 );
	mSphereShader.uniform( "normalsTex", 2 );
	mSphereShader.uniform( "mvpMatrix", mActiveHeadCam.mMvpMatrix );
	mSphereShader.uniform( "terrainScale", mTerrainScale );
	mSphereShader.uniform( "eyePos", mActiveHeadCam.getEye() );
	mSphereShader.uniform( "fogColor", mFogColor );
	mSphereShader.uniform( "sandColor", mSandColor );
	mSphereShader.uniform( "power", mRoom.getPower() );
	mSphereShader.uniform( "roomDims", mRoom.getDims() );
	mSphereShader.uniform( "texCoord", texCoord );
	mSphereShader.uniform( "sphereRadius", mSphere.getRadius() * 0.45f );
	mSphereShader.uniform( "zoomMulti", mZoomMulti );
	mSphereShader.uniform( "timePer", mRoom.getTimePer() * 1.5f + 0.5f );
	gl::draw( mSphere, 128 );

	for (unsigned int i = 0; i < mRandomSpheres.size(); i++){
		gl::draw(mRandomSpheres[i], 20);
	}
	mSphereShader.unbind();
	gl::drawSphere(Vec3f::zero(), 40.0f);
}

void TerrainApp::drawTerrain()
{
	HeadCam *thisViewsCam = getWindow()->getUserData<HeadCam>();

	mRd.getHeightsTexture().bind( 0 );
	mRd.getNormalsTexture().bind( 1 );
	mGradientTex.bind( 2 );
	mSandNormalTex.bind( 3 );
	mTerrainShader.bind();
	mTerrainShader.uniform( "heightsTex", 0 );
	mTerrainShader.uniform( "normalsTex", 1 );
	mTerrainShader.uniform( "gradientTex", 2 );
	mTerrainShader.uniform( "sandNormalTex", 3 );
	mTerrainShader.uniform( "mvpMatrix", mActiveHeadCam.mMvpMatrix );
	mTerrainShader.uniform( "terrainScale", mTerrainScale );
	mTerrainShader.uniform( "roomDims", mRoom.getDims() );
	mTerrainShader.uniform( "zoomMulti", mZoomMulti );//lerp( mZoomMulti, 1.0f, mRoom.getPower() ) );
	mTerrainShader.uniform( "power", mRoom.getPower() );
	mTerrainShader.uniform( "eyePos", mActiveHeadCam.getEye() );
	mTerrainShader.uniform( "lightPos", mLightPos );
	mTerrainShader.uniform( "fogColor", mFogColor );
	mTerrainShader.uniform( "sandColor", mSandColor );
	mTerrainShader.uniform( "mousePosNorm", -( mMousePosNorm - Vec2f( 0.5f, 0.5f ) ) * getElapsedSeconds() * 2.0f );
	mTerrainShader.uniform( "spherePos", mSphere.getCenter() );
	mTerrainShader.uniform( "sphereRadius", mSphere.getRadius() );
	mTerrain.draw();
	mTerrainShader.unbind();
}

void TerrainApp::drawInfoPanel()
{
	gl::pushMatrices();
	gl::translate( mRoom.getDims() );
	gl::scale( Vec3f( -1.0f, -1.0f, 1.0f ) );
	gl::color( Color( 1.0f, 1.0f, 1.0f ) * ( 1.0f - mRoom.getPower() ) );
	gl::enableAlphaBlending();
	
	float iconWidth		= 50.0f;
	
	float X0			= 15.0f;
	float X1			= X0 + iconWidth;
	float Y0			= 300.0f;
	float Y1			= Y0 + iconWidth;
	
	// DRAW ROOM NUM AND DESC
	float c = mRoom.getPower() * 0.5f + 0.5f;
	gl::color( ColorA( c, c, c, 0.5f ) );
	gl::draw( mIconTex, Rectf( X0, Y0, X1, Y1 ) );
	
	c = mRoom.getPower();
	gl::color( ColorA( c, c, c, 0.5f ) );
	gl::disable( GL_TEXTURE_2D );
	
	// DRAW TIME BAR
	float timePer		= mRoom.getTimePer();
	gl::drawSolidRect( Rectf( Vec2f( X0, Y1 + 2.0f ), Vec2f( X0 + timePer * ( iconWidth ), Y1 + 2.0f + 4.0f ) ) );
	
	// DRAW FPS BAR
	float fpsPer		= getAverageFps()/60.0f;
	gl::drawSolidRect( Rectf( Vec2f( X0, Y1 + 4.0f + 4.0f ), Vec2f( X0 + fpsPer * ( iconWidth ), Y1 + 4.0f + 6.0f ) ) );
	
	
	gl::popMatrices();
}


// The window-specific data for each window
class WindowData {
  public:
	WindowData()
		: mColor( Color( CM_HSV, randFloat(), 0.8f, 0.8f ) ) // a random color
	{}
  
	Color			mColor;
	list<Vec2f>		mPoints; // the points drawn into this window
};


void TerrainApp::createNewWindow()
{
	app::WindowRef newWindow = createWindow( Window::Format().size( APP_WIDTH, APP_HEIGHT ) );
	mHeadCam1 = HeadCam( -1000.0f, getWindowAspectRatio() );
	newWindow->setUserData( &mHeadCam1  ); //new WindowData );
	
	// for demonstration purposes, we'll connect a lambda unique to this window which fires on close
	int uniqueId = getNumWindows();
	newWindow->getSignalClose().connect(
			[uniqueId,this] { this->console() << "You closed window #" << uniqueId << std::endl; }
		);
}

void TerrainApp::adjustProjection(Vec3f bottomLeft, Vec3f bottomRight, Vec3f topLeft, Vec3f eyePos, float n, float f)
{
	Vec3f va, vb, vc;
	Vec3f vr, vu, vn;
	float l, r, b, t, d;
	Matrix44f M;
	// Compute an orthonormal basis for the screen.
	vr = bottomRight - bottomLeft;
	vu = topLeft - bottomLeft;
	vr.normalize();
	vu.normalize();
	vn = vr.cross(vu);
	vn.normalize();
	// Compute the screen corner vectors.
	va = bottomLeft - eyePos;
	vb = bottomRight - eyePos;
	vc = topLeft - eyePos;
	// Find the distance from the eye to screen plane.
	d = -va.dot(vn);
	// Find the extent of the perpendicular projection.
	l = vr.dot(va) * n / d;
	r = vr.dot(vb) * n / d;
	b = vu.dot(va) * n / d;
	t = vu.dot(vc) * n / d;
	// Load the perpendicular projection.
	glMatrixMode(GL_PROJECTION);
	glLoadIdentity();
	glFrustum(l, r, b, t, n, f);
	// Rotate the projection to be non-perpendicular.
	memset(M, 0, 16 * sizeof (float));
	M[0] = vr[0]; M[4] = vr[1]; M[ 8] = vr[2];
	M[1] = vu[0]; M[5] = vu[1]; M[ 9] = vu[2];
	M[2] = vn[0]; M[6] = vn[1]; M[10] = vn[2];
	M[15] = 1.0f;
	glMultMatrixf(M);
	// Move the apex of the frustum to the origin.
	glTranslatef(-eyePos[0], -eyePos[1], -eyePos[2]);
	glMatrixMode(GL_MODELVIEW);
}


CINDER_APP_BASIC( TerrainApp, RendererGl )
