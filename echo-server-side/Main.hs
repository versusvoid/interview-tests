{-# LANGUAGE BangPatterns, OverloadedStrings #-}
import qualified Data.List      as L
import qualified Data.ByteString as B
import           Network.Socket hiding (send, sendTo, recv, recvFrom)
import           Network.Socket.ByteString
import           Network.BSD 
import           Data.Maybe as MB
import           System.Environment(getArgs) as Env
import           System.Exit(exitFailure)
import           System.Time
import           Control.Monad
import           Control.Concurrent

data Message = Ping | Alive | FineThanks | IMTheKing
    deriving (Eq, Show, Ord, Enum)

enumToMessageList = 
    [
        (Ping, "PING"), (Alive, "ALIVE?"), 
        (FineThanks, "FINETHANKS"), 
        (IMTheKing, "IMTHEKING")
    ]

enumToMessage = fromJust . flip lookup enumToMessageList

messageToEnumList = map (\(x,y) -> (y,x)) enumToMessageList

messageToEnum = fromJust . flip lookup messageToEnumList

data NodeHandle = 
     NodeHandle {nhSocket  :: Socket,
                 nhId      :: Int,
                 nhAddress :: SockAddr,
                 nhNodes   :: [(Int, SockAddr)],
                 nhKing    :: Int
                } 
microSecDelay = 1000*1000
delayDiff = normalizeTimeDiff $ TimeDiff{tdPicosec = microSecDelay*1000000}

openSock :: IO NodeHandle
openSock = do 
    args <- fmap (map $ L.groupBy (\_ c -> c /= ':')) getArgs
    when (length args < 2) exitFailure
    let ![myId, myHost, myPort] = head addrs
    addrinfos <- getAddrInfo Nothing (Just hostName) (Just port)
    let serveraddr = head addrinfos
    sock <- socket (addrFamily serveraddr) Datagram defaultProtocol
    setSocketOption sock RecvTimeOut (microSecDelay*1000)

    nodes <- forM (tail addrs) $ \[index, ':':host, ':':port'] -> do
                hostId <- inet_addr host
                return $! (read index, SockAddrInet (PortNum $ read port') hostId) 
    return $! NodeHandle sock (read myId) (addrAddress serveraddr) nodes (foldl1 (max `on` fst) nodes)

main :: IO ()
main = do
    nh <- openSock
    if nhId nh == 
       foldl1 (max `on` fst) (nhNode nh)
      then iAmKing nh
      else processQueue nh 

iAmKing nh{nhSocket = sock, nhNodes = nodes, nhId = myId} = do
    forM_ nodes $ \(i,addr) ->
        when (i /= myId) $ do
           sendTo sock "IAMTHEKING" addr
    processQueue nh Nothing

   threadDelay (microSecDelay*4)

processQueue nh{nhSock = sock} lastPongTime = do
    beginTime <- getClockTime
    nh' <-if beginTime > addToClockTime delayDiff lastPongTime
            then elect nh
            else return nh
    when (nhId nh' /= nhKing nh') $ do
        sendTo sock "PING" (fromJust $ lookup nodes kId)
   (message, len, addr) <- recvFrom sock 20
   case message of
        "PONG"   -> getClockTime >> (processQueue nh . Just)
        "ALIVE?" -> (sendTo sock "FINETHANKS") >> elect nh
        "IAMTHEKING" -> processQueue nh{nhKing = 
            MB.fromJust $ 
            foldl (\p (i,a) -> 
                if a == addr 0then Just i else Nothing) $ nhNodes nh}
        "" ->  

