{-# LANGUAGE BangPatterns #-}
import Data.Bits
import Data.List
import Network.Socket
import Network.BSD
import Data.Maybe
import System.Environment
import System.Exit
import Control.Monad

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
     NodeHandle {slSocket  :: Socket,
                 slAddress :: SockAddr,
                 slNodes   :: [(Int, SockAddr)] 
                } 
delay = 1000

main :: IO ()
main = undefined

openSock :: IO NodeHandle
openSock = do 
    args <- getArgs
    when (length args < 2) exitFailure
    let !([hostName, ':':port]:addrs) = map (groupBy (\_ c -> c /= ':')) args
    nodes <- forM addrs $ \[index, ':':host, ':':port'] -> do
                hostId <- inet_addr host
                return $! (read index, SockAddrInet (PortNum $ read port') hostId) 
    addrinfos <- getAddrInfo Nothing (Just hostName) (Just port)
    let serveraddr = head addrinfos
    sock <- socket (addrFamily serveraddr) Datagram defaultProtocol

    return $! NodeHandle sock (addrAddress serveraddr) nodes

